using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateApi.DTOs;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/properties")]
public class PropertiesController(
    IPropertyService propertyService,
    INotificationService notifications,
    IEmailService email,
    ISmsTemplateService templates) : ControllerBase
{
    /// <summary>Render an app SmsTemplate by key and send via WhatsApp/SMS smart router.</summary>
    private async Task SendIfRendered(string toPhone, string key, IDictionary<string, string?> vars, bool preferWhatsApp = false)
    {
        var body = await templates.RenderAsync(key, vars);
        if (!string.IsNullOrWhiteSpace(body))
            await notifications.SendAsync(toPhone, body, preferWhatsApp);
    }
    private async Task BroadcastIfRendered(string key, IDictionary<string, string?> vars)
    {
        var body = await templates.RenderAsync(key, vars);
        if (!string.IsNullOrWhiteSpace(body))
            await notifications.NotifyAdminsAsync(body);
    }
    private int? CurrentUserId =>
        User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;

    private bool IsAdmin =>
        User.FindFirstValue(ClaimTypes.Role) == "Admin";

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PropertyQueryParams q)
    {
        // Anonymous visitors only see Video Promotion (paid) listings — Free
        // listings are reserved for signed-in buyers, which both protects
        // sellers' direct contact details and gives us a stronger reason
        // for buyers to register. Admins and any logged-in user see all tiers.
        if (CurrentUserId is null)
            q.MarketingPlan = "VideoPromotion";

        var result = await propertyService.GetAllAsync(q);
        return Ok(result);
    }

    [HttpGet("featured")]
    public async Task<IActionResult> GetFeatured()
    {
        var anonymous = CurrentUserId is null;
        var result = await propertyService.GetFeaturedAsync();
        if (anonymous)
            result = result.Where(p => p.MarketingPlan == "VideoPromotion").ToList();
        return Ok(result);
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPending()
    {
        var result = await propertyService.GetPendingAsync();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await propertyService.GetByIdAsync(id);
        if (result is null) return NotFound();

        // Free listings are gated behind login — return 401 so the frontend
        // can route the user to /register?intent=buyer instead of leaking
        // the property details.
        if (CurrentUserId is null && result.MarketingPlan != "VideoPromotion")
            return Unauthorized(new { code = "sign_in_required", message = "Sign in to view free listings." });

        return Ok(result);
    }

    [HttpGet("{id:int}/related")]
    public async Task<IActionResult> GetRelated(int id)
    {
        var result = await propertyService.GetRelatedAsync(id);
        if (CurrentUserId is null)
            result = result.Where(p => p.MarketingPlan == "VideoPromotion").ToList();
        return Ok(result);
    }

    /// <summary>
    /// Submit a property. Logged-in admins auto-approve; everyone else
    /// (including anonymous public submissions) goes into the pending queue.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreatePropertyRequest req)
    {
        var autoApprove = IsAdmin;
        var result = await propertyService.CreateAsync(req, CurrentUserId, autoApprove);

        // Send confirmation SMS to the seller / submitter
        var phone = req.SubmitterPhone;
        var name = req.SubmitterName ?? "there";
        var emailAddr = req.SubmitterEmail;

        if (!string.IsNullOrEmpty(phone))
        {
            // Auto-approved (admin) → property.approved; otherwise → property.submittedConfirmation
            var key = autoApprove ? "property.approved" : "property.submittedConfirmation";
            await SendIfRendered(phone, key, new Dictionary<string, string?>
            {
                ["name"] = name,
                ["title"] = result.Title,
            }, preferWhatsApp: true);
        }

        // Email confirmation when an email was provided
        if (!string.IsNullOrEmpty(emailAddr))
        {
            var subject = autoApprove
                ? "Your property is now live on Jose For Land"
                : "We've received your property submission";
            var body =
                $"<p>Hi {name},</p>" +
                $"<p>Thank you for submitting <strong>{result.Title}</strong> on Jose For Land.</p>" +
                (autoApprove
                    ? "<p>Your listing is <strong>now live</strong> and visible to buyers across Kanyakumari.</p>"
                    : "<p>Our team will personally review the listing within 24 hours and reach out to you on " +
                      $"<strong>{phone ?? "your registered phone"}</strong>.</p>") +
                "<p>For urgent help, call <strong>+91 99944 88490</strong>.</p>" +
                "<p>— The Jose For Land team</p>";
            await email.SendAsync(emailAddr, subject, body);
        }

        // Notify admins of a new pending submission — uses property.adminPending template
        if (!autoApprove)
        {
            await BroadcastIfRendered("property.adminPending", new Dictionary<string, string?>
            {
                ["title"] = result.Title,
                ["priceLakhs"] = (result.TotalPrice / 100000m).ToString("F2"),
                ["area"] = result.AreaInCents.ToString(),
                ["name"] = string.IsNullOrEmpty(phone) ? "(unknown)" : name,
                ["phone"] = phone ?? "",
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Admins can edit any property; everyone else can only edit ones they own
    /// (submitted by them or assigned to them as agent).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePropertyRequest req)
    {
        if (IsAdmin)
        {
            var result = await propertyService.UpdateAsync(id, req);
            return result is null ? NotFound() : Ok(result);
        }

        var asOwner = await propertyService.UpdateAsOwnerAsync(id, CurrentUserId!.Value, req);
        if (asOwner is null) return Forbid();
        return Ok(asOwner);
    }

    /// <summary>List properties submitted by (or assigned to) the current user.</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine() =>
        Ok(await propertyService.GetMineAsync(CurrentUserId!.Value));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await propertyService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveOrReject(int id, [FromBody] ApprovalRequest req)
    {
        if (req.Action.ToLower() is not ("approve" or "reject"))
            return BadRequest(new { message = "Action must be 'approve' or 'reject'" });

        var result = await propertyService.ApproveOrRejectAsync(id, req);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Admin assigns a pending property to an Employee/Agent/Admin for the
    /// verification step (site visit, document check, photos). Triggers a
    /// WhatsApp/SMS to the assignee.
    /// </summary>
    [HttpPatch("{id:int}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignToVerify(int id, [FromBody] AssignPropertyRequest req)
    {
        try
        {
            var result = await propertyService.AssignToVerifyAsync(id, req.AssignedToUserId);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/favorite")]
    [Authorize]
    public async Task<IActionResult> ToggleFavorite(int id)
    {
        var userId = CurrentUserId!.Value;
        var saved = await propertyService.ToggleFavoriteAsync(id, userId);
        return Ok(new { saved });
    }

    /// <summary>List the current user's saved/favorite properties.</summary>
    [HttpGet("favorites")]
    [Authorize]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = CurrentUserId!.Value;
        return Ok(await propertyService.GetFavoritesAsync(userId));
    }
}
