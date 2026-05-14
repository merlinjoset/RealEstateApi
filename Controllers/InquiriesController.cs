using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTOs;
using RealEstateApi.Models;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/inquiries")]
public class InquiriesController(
    AppDbContext db,
    INotificationService notifications,
    ISmsTemplateService templates) : ControllerBase
{
    /// <summary>
    /// Render an SmsTemplate from the database and send it. Tries WhatsApp first
    /// when <paramref name="preferWhatsApp"/> is true, falls back to SMS automatically.
    /// </summary>
    private async Task SendIfRendered(string toPhone, string templateKey,
        IDictionary<string, string?> vars, bool preferWhatsApp = false)
    {
        var body = await templates.RenderAsync(templateKey, vars);
        if (!string.IsNullOrWhiteSpace(body))
            await notifications.SendAsync(toPhone, body, preferWhatsApp);
    }

    /// <summary>Render once, broadcast to every active admin (WhatsApp first, SMS fallback).</summary>
    private async Task BroadcastIfRendered(string templateKey, IDictionary<string, string?> vars)
    {
        var body = await templates.RenderAsync(templateKey, vars);
        if (!string.IsNullOrWhiteSpace(body))
            await notifications.NotifyAdminsAsync(body);
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInquiryRequest req)
    {
        if (!Enum.TryParse<PreferredContact>(req.PreferredContact, true, out var pc))
            pc = PreferredContact.Phone;
        if (!Enum.TryParse<InquiryType>(req.Type ?? "General", true, out var type))
            type = InquiryType.General;

        var inquiry = new Inquiry
        {
            Name = req.Name,
            Phone = req.Phone,
            Email = req.Email,
            Message = req.Message,
            PreferredContact = pc,
            Type = type,
            PropertyId = req.PropertyId,
            Status = InquiryStatus.New,
        };

        db.Inquiries.Add(inquiry);
        await db.SaveChangesAsync();

        // Look up the property title once — used in both the admin broadcast
        // and the visitor's confirmation so they can see which listing this
        // inquiry was about.
        string? propertyTitle = null;
        if (req.PropertyId is int pid)
        {
            propertyTitle = await db.Properties
                .Where(p => p.Id == pid)
                .Select(p => p.Title)
                .FirstOrDefaultAsync();
        }
        var propertyContext = propertyTitle is not null
            ? $" regarding \"{propertyTitle}\""
            : req.PropertyId is null ? "" : $" re property #{req.PropertyId}";

        // Pick a more specific template when this is a document request,
        // otherwise fall back to the general adminNotification.
        var adminTemplateKey = type == InquiryType.DocumentRequest
            ? "inquiry.documentRequest"
            : "inquiry.adminNotification";

        await BroadcastIfRendered(adminTemplateKey, new Dictionary<string, string?>
        {
            ["name"] = req.Name,
            ["phone"] = req.Phone,
            ["type"] = type.ToString(),
            ["propertyContext"] = propertyContext,
            ["propertyId"] = req.PropertyId?.ToString() ?? "",
            ["propertyTitle"] = propertyTitle ?? "",
        });

        // Visitor confirmation. Pick a document-request-specific template
        // when they came in via "Request to view documents" so the SMS calls
        // that out explicitly; otherwise the generic confirmation.
        var visitorTemplateKey = type == InquiryType.DocumentRequest
            ? "inquiry.documentRequestConfirmation"
            : "inquiry.confirmation";

        await SendIfRendered(req.Phone, visitorTemplateKey,
            new Dictionary<string, string?>
            {
                ["name"] = req.Name,
                ["phone"] = req.Phone,
                ["propertyId"] = req.PropertyId?.ToString() ?? "",
                ["propertyTitle"] = propertyTitle ?? "",
                ["propertyContext"] = propertyContext,
            },
            preferWhatsApp: pc == PreferredContact.WhatsApp);

        return Created("", new { id = inquiry.Id, message = "Inquiry sent successfully" });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int? assignedTo,
        [FromQuery] int? propertyId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = db.Inquiries
            .Include(i => i.Property)
            .Include(i => i.AssignedToUser)
            .AsQueryable();

        if (unreadOnly == true) query = query.Where(i => !i.IsRead);
        if (assignedTo is int aId) query = query.Where(i => i.AssignedToUserId == aId);
        if (propertyId is int pId) query = query.Where(i => i.PropertyId == pId);
        if (!string.IsNullOrWhiteSpace(type) && type != "all"
            && Enum.TryParse<InquiryType>(type, true, out var ty))
            query = query.Where(i => i.Type == ty);
        if (!string.IsNullOrWhiteSpace(status) && status != "all"
            && Enum.TryParse<InquiryStatus>(status, true, out var st))
            query = query.Where(i => i.Status == st);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InquiryDto(
                i.Id, i.Name, i.Phone, i.Email, i.Message,
                i.PreferredContact.ToString(), i.Type.ToString(), i.IsRead,
                i.Status.ToString(), i.Notes,
                i.PropertyId, i.Property != null ? i.Property.Title : null,
                i.AssignedToUserId,
                i.AssignedToUser != null ? i.AssignedToUser.FirstName + " " + i.AssignedToUser.LastName : null,
                i.AssignedAt, i.LastUpdatedAt, i.CreatedAt))
            .ToListAsync();

        return Ok(new { data = items, total, page, pageSize });
    }

    /// <summary>
    /// Inquiries assigned to the current user. Backs the Employee "My Work"
    /// page — returns a raw array (not paginated) since the volume per user
    /// is small.
    /// </summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMineAssigned()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var items = await db.Inquiries
            .Include(i => i.Property)
            .Include(i => i.AssignedToUser)
            .Where(i => i.AssignedToUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InquiryDto(
                i.Id, i.Name, i.Phone, i.Email, i.Message,
                i.PreferredContact.ToString(), i.Type.ToString(), i.IsRead,
                i.Status.ToString(), i.Notes,
                i.PropertyId, i.Property != null ? i.Property.Title : null,
                i.AssignedToUserId,
                i.AssignedToUser != null
                    ? i.AssignedToUser.FirstName + " " + i.AssignedToUser.LastName
                    : null,
                i.AssignedAt, i.LastUpdatedAt, i.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var i = await db.Inquiries
            .Include(x => x.Property)
            .Include(x => x.AssignedToUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (i is null) return NotFound();

        return Ok(new InquiryDto(
            i.Id, i.Name, i.Phone, i.Email, i.Message,
            i.PreferredContact.ToString(), i.Type.ToString(), i.IsRead,
            i.Status.ToString(), i.Notes,
            i.PropertyId, i.Property?.Title,
            i.AssignedToUserId,
            i.AssignedToUser != null ? i.AssignedToUser.FirstName + " " + i.AssignedToUser.LastName : null,
            i.AssignedAt, i.LastUpdatedAt, i.CreatedAt));
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var inquiry = await db.Inquiries.FindAsync(id);
        if (inquiry is null) return NotFound();
        inquiry.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Admin assigns an inquiry to an Employee. SMS broadcast goes to that employee.
    /// </summary>
    [HttpPatch("{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignInquiryRequest req)
    {
        var inquiry = await db.Inquiries.FindAsync(id);
        if (inquiry is null) return NotFound();

        var assignee = await db.Users.FindAsync(req.AssignedToUserId);
        if (assignee is null) return BadRequest(new { message = "Assignee not found" });
        if (assignee.Role != UserRole.Employee || !assignee.IsActive)
            return BadRequest(new { message = "Inquiries can only be assigned to active Employees." });

        inquiry.AssignedToUserId = assignee.Id;
        inquiry.AssignedAt = DateTime.UtcNow;
        inquiry.Status = InquiryStatus.Assigned;
        await db.SaveChangesAsync();

        // Notify assignee — uses inquiry.assignment template, prefers WhatsApp
        if (!string.IsNullOrEmpty(assignee.Phone))
        {
            await SendIfRendered(assignee.Phone, "inquiry.assignment",
                new Dictionary<string, string?>
                {
                    ["name"] = inquiry.Name,
                    ["phone"] = inquiry.Phone,
                },
                preferWhatsApp: true);
        }

        return await GetById(id);
    }

    /// <summary>
    /// Employee/Agent updates inquiry status + notes. Admin gets SMS notification.
    /// </summary>
    [HttpPatch("{id:int}/update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInquiryRequest req)
    {
        if (!Enum.TryParse<InquiryStatus>(req.Status, true, out var newStatus))
            return BadRequest(new { message = $"Invalid status: {req.Status}" });

        var inquiry = await db.Inquiries
            .Include(i => i.AssignedToUser)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (inquiry is null) return NotFound();

        var prevStatus = inquiry.Status;
        inquiry.Status = newStatus;
        inquiry.Notes = req.Notes;
        inquiry.LastUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Admin broadcast — uses inquiry.statusUpdate template
        var actor = inquiry.AssignedToUser is null
            ? "An employee"
            : $"{inquiry.AssignedToUser.FirstName} {inquiry.AssignedToUser.LastName}";
        var noteSuffix = string.IsNullOrWhiteSpace(req.Notes)
            ? ""
            : $" · note: {Trunc(req.Notes!, 60)}";

        await BroadcastIfRendered("inquiry.statusUpdate", new Dictionary<string, string?>
        {
            ["id"] = inquiry.Id.ToString(),
            ["name"] = inquiry.Name,
            ["actor"] = actor,
            ["prevStatus"] = prevStatus.ToString(),
            ["newStatus"] = newStatus.ToString(),
            ["noteSuffix"] = noteSuffix,
        });

        return await GetById(id);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var inquiry = await db.Inquiries.FindAsync(id);
        if (inquiry is null) return NotFound();
        db.Inquiries.Remove(inquiry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string Trunc(string s, int n) =>
        s.Length <= n ? s : s[..n] + "…";
}
