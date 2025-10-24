using DominationPoint.Core.Domain;

namespace DominationPoint.Core.Application
{
    public interface IMapAnnotationService
    {
        Task<List<MapAnnotation>> GetAnnotationsForGameAsync(int gameId);
        Task SetAnnotationAsync(int gameId, int x, int y, string text);
    }
}
