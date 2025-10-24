using System.ComponentModel.DataAnnotations;

namespace DominationPoint.Models
{
    public class CreateTeamViewModel
    {
        [Required]
        [Display(Name = "Team Name")]
        [StringLength(50, MinimumLength = 3)]
        public string TeamName { get; set; } = null!;

        [Required]
        [Display(Name = "Team Color")]
        public string ColorHex { get; set; } = "#ff0000"; 
    }
}
