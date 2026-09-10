using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreUpdateActiveStatusVM
    {
        [Required]
        public int? StoreId { get; set; }

        [Required]
        public bool? IsActive { get; set; }
    }
}
