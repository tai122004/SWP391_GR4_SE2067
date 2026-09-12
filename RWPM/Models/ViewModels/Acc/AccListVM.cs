using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Acc
{
    public class AccListVM
    {
        public PaginationRes<Entities.Acc> PanigationResponse { get; set; }
        public string? Search { get; set; }
        public string? IsActive { get; set; }

        public AccListVM(PaginationRes<Entities.Acc> panigationResponse, AccSearch searchObject)
        {
            PanigationResponse = panigationResponse;
            Search = searchObject.Search;
            IsActive = searchObject.IsActive;
        }
    }
}