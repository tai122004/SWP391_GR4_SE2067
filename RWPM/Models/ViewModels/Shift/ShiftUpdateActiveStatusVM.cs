using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftUpdateActiveStatusVM
    {
        [Required]
        public int? ShiftId { get; set; }

        [Required]
        public bool? IsActive { get; set; }
    }
}
