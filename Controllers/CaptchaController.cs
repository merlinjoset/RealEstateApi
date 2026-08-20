using Microsoft.AspNetCore.Mvc;
using RealEstateApi.Services;

namespace RealEstateApi.Controllers;

[ApiController]
[Route("api/captcha")]
public class CaptchaController(ICaptchaService captcha) : ControllerBase
{
    /// <summary>Issue a fresh CAPTCHA challenge for the public forms.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        var (token, image) = captcha.Generate();
        return Ok(new { token, image });
    }
}
