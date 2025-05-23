using Microsoft.AspNetCore.Mvc;
using agent_with_tool_V0.services;

namespace agent_with_tool_V0.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly PrivateAgent _agent;
        public ChatController(PrivateAgent agent) { _agent = agent; }

        public class ChatRequest
        {
            public string? Message { get; set; }
            public string? ThreadId { get; set; }
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Message))
            {
                return BadRequest("Message is required.");
            }
            var reply = await _agent.GetResponseAsync(req.Message, req.ThreadId);
            return Content(reply ?? string.Empty, "text/plain");
        }

        [HttpPost("thread")]
        public async Task<IActionResult> CreateThread()
        {
            var threadId = await _agent.CreateThreadAsync();
            if (threadId.StartsWith("[Error]:"))
            {
                // Return 500 with error details for debugging
                return StatusCode(500, new { error = threadId });
            }
            if (string.IsNullOrWhiteSpace(threadId))
            {
                threadId = Guid.NewGuid().ToString();
            }
            return Ok(new { threadId });
        }

        [HttpGet("/api/health")]
        public IActionResult Health() => Ok(new { status = "ok" });
    }
}
