namespace DominationPoint.Core.Domain
{
    public class Game
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public GameStatus Status { get; set; }

        // Navigation properties
        public ICollection<ControlPoint> ControlPoints { get; set; } = new List<ControlPoint>();
        public ICollection<MapAnnotation> MapAnnotations { get; set; } = new List<MapAnnotation>();
        public ICollection<GameScore> GameScores { get; set; } = new List<GameScore>();
        public ICollection<GameParticipant> GameParticipants { get; set; } = new List<GameParticipant>();
        public ICollection<GameEvent> GameEvents { get; set; } = new List<GameEvent>();

    }

    public enum GameStatus
    {
        Scheduled,
        Active,
        Finished
    }
}
