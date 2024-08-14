using Microsoft.AspNetCore.Mvc;

namespace WebRTCWebSocketServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StreamController(ILogger<StreamController> logger) : ControllerBase
    {
        private readonly ILogger<StreamController> _logger = logger;
    }
}