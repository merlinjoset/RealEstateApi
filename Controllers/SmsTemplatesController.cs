using Microsoft.AspNetCore.Mvc;
using RealEstateApi.DTOs;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/sms-templates")]
public class SmsTemplatesController(
    ISmsTemplateService templates,
    ISmsService sms,
    IConfiguration config) : ControllerBase
{
    /// <summary>
    /// Render the template with realistic sample data and send to the supplied
    /// phone — useful for verifying SMS delivery end-to-end.
    /// </summary>
    [HttpPost("{id:int}/test")]
    public async Task<ActionResult<TestSmsResult>> SendTest(int id, [FromBody] TestSmsRequest req)
    {
        var tpl = await templates.GetByIdAsync(id);
        if (tpl is null) return NotFound();

        // Sensible defaults that match the seeded variable list
        var defaults = new Dictionary<string, string?>
        {
            ["name"] = "Rajan Kumar",
            ["phone"] = req.Phone,
            ["title"] = "15 Cents Land - Nagercoil",
            ["id"] = "101",
            ["actor"] = "Sundaram Pillai",
            ["prevStatus"] = "Assigned",
            ["newStatus"] = "InProgress",
            ["priceLakhs"] = "22.50",
            ["area"] = "15",
            ["propertyContext"] = " re property #1",
            ["propertyId"] = "1",
            ["noteSuffix"] = " · note: Site visit scheduled for Sat",
            ["reasonSuffix"] = " Reason: Insufficient documents.",
        };

        // Caller-supplied vars override defaults
        if (req.Vars is not null)
            foreach (var (k, v) in req.Vars) defaults[k] = v;

        var rendered = await templates.RenderAsync(tpl.Key, defaults);
        if (string.IsNullOrWhiteSpace(rendered))
            return BadRequest(new { message = "Template is disabled or could not render." });

        await sms.SendAsync(req.Phone, rendered);

        var hasFast2SmsKey = !string.IsNullOrWhiteSpace(config["Sms:Fast2Sms:AuthKey"]);
        var actualProvider = hasFast2SmsKey
            ? "Fast2SMS"
            : "Console (no provider key set — check API logs)";

        return Ok(new TestSmsResult(
            Sent: true,
            Provider: actualProvider,
            RenderedBody: rendered,
            Phone: req.Phone,
            Note: actualProvider.StartsWith("Console")
                ? "No real SMS provider configured — message logged to API console."
                : $"Sent via {actualProvider}. Check the recipient's SMS inbox."
        ));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SmsTemplateDto>>> GetAll(CancellationToken ct)
        => Ok(await templates.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SmsTemplateDto>> GetById(int id, CancellationToken ct)
    {
        var t = await templates.GetByIdAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost]
    public async Task<ActionResult<SmsTemplateDto>> Create([FromBody] CreateSmsTemplateRequest req, CancellationToken ct)
    {
        try
        {
            var created = await templates.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex.InnerException?.Message.Contains("unique") == true)
        {
            return Conflict(new { message = "A template with this key already exists." });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SmsTemplateDto>> Update(int id, [FromBody] UpdateSmsTemplateRequest req, CancellationToken ct)
    {
        var updated = await templates.UpdateAsync(id, req, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPatch("{id:int}/toggle")]
    public async Task<ActionResult<SmsTemplateDto>> Toggle(int id, CancellationToken ct)
    {
        var t = await templates.ToggleActiveAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await templates.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
