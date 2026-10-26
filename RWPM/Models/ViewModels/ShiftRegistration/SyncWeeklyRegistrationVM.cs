using RWPM.Common.Attributes;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class SyncWeeklyRegistrationVM
    {
        [RequiredLocalization]
        public DateTime StartOfWeek { get; set; }
        
        public List<ShiftRegistrationCreateVM> Registrations { get; set; } = new List<ShiftRegistrationCreateVM>();
    }
}
