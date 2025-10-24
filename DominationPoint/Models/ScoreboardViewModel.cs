using DominationPoint.Core.Domain;

namespace DominationPoint.Models
{
    public class ScoreboardViewModel
    {
        public Game Game { get; set; } = null!;
        public List<TeamScore> TeamScores { get; set; } = new();
    }

    public class TeamScore
    {
        public string TeamName { get; set; } = string.Empty;
        public string TeamColorHex { get; set; } = "#FFFFFF";
        public int HoldingScore { get; set; }
        public int CaptureBonusScore { get; set; }

        public int TotalScoreFromDb { get; set; }

        public int TotalScore => TotalScoreFromDb > 0 ? TotalScoreFromDb : HoldingScore + CaptureBonusScore;
    }
}
