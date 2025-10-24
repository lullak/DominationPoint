namespace DominationPoint.Core.Domain
{
    public class GameParticipant
    {
        public int GameId { get; set; }
        public Game Game { get; set; } = null!;

        public string ApplicationUserId { get; set; } = null!;
        public ApplicationUser ApplicationUser { get; set; } = null!;
    }
}
