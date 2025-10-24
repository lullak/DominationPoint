using DominationPoint.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace DominationPoint.Core.Application
{
    public interface IApplicationDbContext
    {
        DbSet<ControlPoint> ControlPoints { get; }
        DbSet<Game> Games { get; }
        DbSet<GameScore> GameScores { get; }
        DbSet<MapAnnotation> MapAnnotations { get; }
        DbSet<GameParticipant> GameParticipants { get; }
        DbSet<GameEvent> GameEvents { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
