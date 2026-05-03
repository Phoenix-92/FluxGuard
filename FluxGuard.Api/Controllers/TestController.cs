namespace FluxGuard.Api.Controllers;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new { message = "pong", timestamp = DateTime.UtcNow });
    }

    [HttpGet("data")]
    public IActionResult GetData()
    {
        return Ok(new { data = "Sensitive business data", timestamp = DateTime.UtcNow });
    }
}