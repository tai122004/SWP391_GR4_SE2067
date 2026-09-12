using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftListVM
    {
        public PaginationRes<Entities.Shift> PanigationResponse { get; set; }
        public string? Search { get; set; }
        public bool? IsActive { get; set; }

        public ShiftListVM(PaginationRes<Entities.Shift> panigationResponse, ShiftSearch searchObject)
        {
            PanigationResponse = panigationResponse;
            Search = searchObject.Search;
            IsActive = searchObject.IsActive;
        }
    }
}
