using DominationPoint.Core.Application;
using DominationPoint.Core.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DominationPoint.Infrastructure
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ControlPoint> ControlPoints => Set<ControlPoint>();
        public DbSet<Game> Games => Set<Game>();
        public DbSet<GameScore> GameScores => Set<GameScore>();
        public DbSet<MapAnnotation> MapAnnotations => Set<MapAnnotation>();
        public DbSet<GameParticipant> GameParticipants => Set<GameParticipant>();
        public DbSet<GameEvent> GameEvents => Set<GameEvent>();

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<GameParticipant>()
                .HasKey(gp => new { gp.GameId, gp.ApplicationUserId });
        }
    }
}
