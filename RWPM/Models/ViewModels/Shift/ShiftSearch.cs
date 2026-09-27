using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftSearch : PagedReq
    {
        public ShiftSearch()
        {
            PageSize = 10;
        }

        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsTemplate { get; set; }
        public int? StoreId { get; set; }
        public DateTime? EffectiveOn { get; set; }
    }
}
