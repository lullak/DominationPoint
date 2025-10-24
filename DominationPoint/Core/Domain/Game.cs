namespace DominationPoint.Core.Domain
{
    public class Game
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public GameStatus Status { get; set; }
    }

    public enum GameStatus
    {
        Scheduled,
        Active,
        Finished
    }
}
