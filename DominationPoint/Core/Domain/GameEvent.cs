namespace DominationPoint.Core.Domain
{
    public class GameEvent
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public Game Game { get; set; } = null!;

        public int ControlPointId { get; set; }

        public EventType Type { get; set; }
        public DateTime Timestamp { get; set; }

        public string? ActingUserId { get; set; }
        public ApplicationUser? ActingUser { get; set; }

        public string? PreviousOwnerUserId { get; set; }
    }

    public enum EventType
    {
        Capture,
        GameEnd 
    }
}
