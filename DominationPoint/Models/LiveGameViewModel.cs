using DominationPoint.Core.Domain;

namespace DominationPoint.Models
{
    public class LiveGameViewModel
    {
        public Game Game { get; set; } = null!;
        public List<GameParticipant> Participants { get; set; } = new();
        public List<ControlPoint> ControlPoints { get; set; } = new();
        public List<MapAnnotation> Annotations { get; set; } = new();
    }
}
