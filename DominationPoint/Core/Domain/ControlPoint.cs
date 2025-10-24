namespace DominationPoint.Core.Domain
{
    public class ControlPoint
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public Game Game { get; set; } = null!; 
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public ControlPointStatus Status { get; set; }

        public string? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }
    }

    public enum ControlPointStatus
    {
        Inactive,
        Controlled
    }
}
