using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftSearch : PagedReq
    {
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsTemplate { get; set; }
    }
}
