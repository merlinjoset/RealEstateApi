using Microsoft.AspNetCore.Mvc;
using RealEstateApi.DTOs;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/testimonials")]
public class TestimonialsController(ITestimonialService testimonials) : ControllerBase
{
    /// <summary>
    /// Public list — returns only published testimonials by default.
    /// Pass ?all=true (admin) to include drafts.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TestimonialDto>>> GetAll([FromQuery] bool all = false)
        => Ok(await testimonials.GetAllAsync(publishedOnly: !all));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TestimonialDto>> GetById(int id)
    {
        var t = await testimonials.GetByIdAsync(id);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost]
    public async Task<ActionResult<TestimonialDto>> Create([FromBody] CreateTestimonialRequest req)
    {
        var created = await testimonials.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TestimonialDto>> Update(int id, [FromBody] UpdateTestimonialRequest req)
    {
        var updated = await testimonials.UpdateAsync(id, req);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPatch("{id:int}/publish")]
    public async Task<ActionResult<TestimonialDto>> TogglePublished(int id)
    {
        var t = await testimonials.TogglePublishedAsync(id);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await testimonials.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }
}
