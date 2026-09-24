using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class SyncWeeklyRegistrationVM
    {
        [Required]
        public DateTime StartOfWeek { get; set; }
        
        public List<ShiftRegistrationCreateVM> Registrations { get; set; } = new List<ShiftRegistrationCreateVM>();
    }
}
