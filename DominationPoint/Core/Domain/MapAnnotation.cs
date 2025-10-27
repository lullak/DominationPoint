namespace DominationPoint.Core.Domain
{
    public class MapAnnotation
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Game Game { get; set; } = null!; 
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public required string Text { get; set; }
    }
}
