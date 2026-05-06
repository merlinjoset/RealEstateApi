using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTOs;
using RealEstateApi.Models;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/inquiries")]
public class InquiriesController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInquiryRequest req)
    {
        if (!Enum.TryParse<PreferredContact>(req.PreferredContact, true, out var pc))
            pc = PreferredContact.Phone;

        var inquiry = new Inquiry
        {
            Name = req.Name,
            Phone = req.Phone,
            Email = req.Email,
            Message = req.Message,
            PreferredContact = pc,
            PropertyId = req.PropertyId,
        };

        db.Inquiries.Add(inquiry);
        await db.SaveChangesAsync();
        return Created("", new { id = inquiry.Id, message = "Inquiry sent successfully" });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll([FromQuery] bool? unreadOnly, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Inquiries.Include(i => i.Property).AsQueryable();
        if (unreadOnly == true) query = query.Where(i => !i.IsRead);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InquiryDto(
                i.Id, i.Name, i.Phone, i.Email, i.Message,
                i.PreferredContact.ToString(), i.IsRead,
                i.PropertyId, i.Property != null ? i.Property.Title : null,
                i.CreatedAt))
            .ToListAsync();

        return Ok(new { data = items, total, page, pageSize });
    }

    [HttpPatch("{id:int}/read")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var inquiry = await db.Inquiries.FindAsync(id);
        if (inquiry is null) return NotFound();
        inquiry.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var inquiry = await db.Inquiries.FindAsync(id);
        if (inquiry is null) return NotFound();
        db.Inquiries.Remove(inquiry);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
