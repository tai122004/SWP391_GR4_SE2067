using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Acc
{
    public class AccUpdateActiveStatusVM
    {
        [Required]
        public string? Username { get; set; }
        [Required]
        public bool? IsActive { get; set; }
    }
}
