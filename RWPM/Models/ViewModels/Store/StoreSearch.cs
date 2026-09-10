using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreSearch : PagedReq
    {
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
    }
}
