using DominationPoint.Core.Domain;

namespace DominationPoint.Models
{
    public class GameDetailViewModel
    {
        public Game Game { get; set; } = null!;
        public List<ApplicationUser> Participants { get; set; } = new();
        public List<ApplicationUser> NonParticipants { get; set; } = new();
        public List<MapAnnotation> Annotations { get; set; } = new();
        public List<ControlPoint> ControlPoints { get; set; } = new();
    }
}
