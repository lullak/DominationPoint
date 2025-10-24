using DominationPoint.Core.Application;
using DominationPoint.Core.Application.Services;
using DominationPoint.Core.Domain;
using DominationPoint.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DominationPoint.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IControlPointService _cpService;
        private readonly IGameManagementService _gameService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMapAnnotationService _annotationService;
        private readonly IScoreboardService _scoreboardService;

        public AdminController(
            IControlPointService cpService,
            IGameManagementService gameService,
            UserManager<ApplicationUser> userManager,
            IMapAnnotationService annotationService,
            IScoreboardService scoreboardService)
        {
            _cpService = cpService;
            _gameService = gameService;
            _userManager = userManager;
            _annotationService = annotationService;
            _scoreboardService = scoreboardService;
        }

        public async Task<IActionResult> Scoreboard(int id)
        {
            var viewModel = await _scoreboardService.GetSavedScoreboardAsync(id);
            return View(viewModel);
        }


        #region Game Management

        public async Task<IActionResult> ManageGames()
        {
            var allGames = await _gameService.GetAllGamesAsync();
            return View(allGames);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGame(string name, DateTime startTime, DateTime endTime)
        {
            if (!string.IsNullOrWhiteSpace(name) && startTime < endTime)
            {
                await _gameService.CreateGameAsync(name, startTime, endTime);
            }
            else
            {
                TempData["ErrorMessage"] = "Game must have a name and start time must be before end time.";
            }
            return RedirectToAction(nameof(ManageGames));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartGame(int id)
        {
            await _gameService.StartGameAsync(id);
            return RedirectToAction(nameof(ManageGames));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndGame(int id)
        {
            await _gameService.EndGameAsync(id);
            return RedirectToAction(nameof(ManageGames));
        }

        #endregion

        #region Game Details & Map Editing

        public async Task<IActionResult> GameDetails(int id)
        {
            var game = await _gameService.GetGameByIdAsync(id);
            if (game == null)
            {
                return NotFound();
            }

            var viewModel = new GameDetailViewModel
            {
                Game = game,
                Participants = await _gameService.GetParticipantsAsync(id),
                NonParticipants = await _gameService.GetNonParticipantsAsync(id),
                Annotations = await _annotationService.GetAnnotationsForGameAsync(id),
                ControlPoints = await _cpService.GetControlPointsForGameAsync(id)
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddParticipant(int gameId, string userId)
        {
            await _gameService.AddParticipantAsync(gameId, userId);
            return RedirectToAction(nameof(GameDetails), new { id = gameId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveParticipant(int gameId, string userId)
        {
            await _gameService.RemoveParticipantAsync(gameId, userId);
            return RedirectToAction(nameof(GameDetails), new { id = gameId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMap(int gameId, int positionX, int positionY, string? text, bool isCp)
        {
            await _annotationService.SetAnnotationAsync(gameId, positionX, positionY, text ?? "");

            await _cpService.ToggleControlPointMarkerAsync(gameId, positionX, positionY, isCp);

            return RedirectToAction(nameof(GameDetails), new { id = gameId });
        }

        #endregion

        #region Live Game Management

        public async Task<IActionResult> LiveGame(int id)
        {
            var game = await _gameService.GetGameByIdAsync(id);
            if (game == null) return NotFound();

            if (game.Status != GameStatus.Active)
            {
                return RedirectToAction(nameof(GameDetails), new { id = id });
            }

            var viewModel = new LiveGameViewModel
            {
                Game = game,
                Participants = await _gameService.GetParticipantsWithIncludesAsync(id),
                ControlPoints = await _cpService.GetControlPointsForGameAsync(id),
                Annotations = await _annotationService.GetAnnotationsForGameAsync(id)
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // BEFORE: public async Task<IActionResult> UpdateLiveTileState(int gameId, int positionX, int positionY, string? ownerId)
        public async Task<IActionResult> UpdateLiveTileState(int gameId, int cpId, string? userId) // AFTER
        {
            // We now use cpId, which is more reliable.
            await _cpService.UpdateControlPointStateAsync(cpId, userId);

            return RedirectToAction(nameof(LiveGame), new { id = gameId });
        }

        #endregion

        #region User (Team) Management

        public async Task<IActionResult> ManageUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        public IActionResult CreateTeam()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeam(CreateTeamViewModel model)
        {
            if (ModelState.IsValid)
            {
                var teamIdentifier = model.TeamName.Replace(" ", "").ToLower();
                var user = new ApplicationUser
                {
                    UserName = model.TeamName,
                    Email = $"{teamIdentifier}@{Request.Host.Value}",
                    ColorHex = model.ColorHex,
                    EmailConfirmed = true
                };

                var randomPassword = $"Pa$$w0rd{Guid.NewGuid()}";
                var result = await _userManager.CreateAsync(user, randomPassword);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Team");
                    TempData["SuccessMessage"] = $"Team '{model.TeamName}' created successfully!";
                    return RedirectToAction(nameof(ManageUsers));
                }

                foreach (var error in result.Errors)
                {
                    if (error.Code.Contains("DuplicateUserName"))
                    {
                        ModelState.AddModelError("TeamName", $"The team name '{model.TeamName}' is already taken.");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNumpadCode(string userId, string numpadCode)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.NumpadCode = numpadCode;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction(nameof(ManageUsers));
        }

        #endregion
    }
}