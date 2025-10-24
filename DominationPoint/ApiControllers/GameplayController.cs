namespace DominationPoint.ApiControllers
{
    using DominationPoint.Core.Application;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;

    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Team")]
    public class GameplayController : ControllerBase
    {
        private readonly IGameplayService _gameplayService;

        public GameplayController(IGameplayService gameplayService)
        {
            _gameplayService = gameplayService;
        }

        public record ActionRequest(string NumpadCode);

        [HttpPost("controlpoints/{id}/capture")]
        public async Task<IActionResult> Capture(int id, [FromBody] ActionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var (success, message) = await _gameplayService.CaptureControlPointAsync(id, userId, request.NumpadCode);

            if (!success) return BadRequest(new { Message = message });

            return Ok(new { Message = message });
        }
    }
}
