using DominationPoint.Core.Application;
using DominationPoint.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace DominationPoint.Core.Application.Services
{
    public class MapAnnotationService : IMapAnnotationService
    {
        private readonly IApplicationDbContext _context;

        public MapAnnotationService(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<MapAnnotation>> GetAllAnnotationsAsync()
        {
            return await _context.MapAnnotations.ToListAsync();
        }

        public async Task<List<MapAnnotation>> GetAnnotationsForGameAsync(int gameId)
        {
            return await _context.MapAnnotations.Where(a => a.GameId == gameId).ToListAsync();
        }

        public async Task SetAnnotationAsync(int gameId, int x, int y, string text)
        {
            var annotation = await _context.MapAnnotations
                .FirstOrDefaultAsync(a => a.GameId == gameId && a.PositionX == x && a.PositionY == y);

            if (!string.IsNullOrEmpty(text) && text.Length > 3)
            {
                text = text.Substring(0, 3);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                if (annotation != null)
                {
                    _context.MapAnnotations.Remove(annotation);
                }
            }
            else
            {
                if (annotation == null)
                {
                    annotation = new MapAnnotation { GameId = gameId, PositionX = x, PositionY = y, Text = text };
                    _context.MapAnnotations.Add(annotation);
                }
                else
                {
                    annotation.Text = text;
                }
            }
            await _context.SaveChangesAsync(default);
        }
    }
}