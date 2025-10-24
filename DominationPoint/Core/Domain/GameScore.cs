namespace DominationPoint.Core.Domain
{
    public class GameScore
    {
        public int Id { get; set; }
        public int Points { get; set; }

        public int GameId { get; set; }
        public Game Game { get; set; } = null!;

        public required string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; } = null!;
    }
}
