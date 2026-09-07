using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Acc
{
    public class AccSearch : PagedReq
    {
        public string? Search { get; set; }
        public int? DeptCatId { get; set; }
        public bool? IsActive { get; set; }

    }
}
