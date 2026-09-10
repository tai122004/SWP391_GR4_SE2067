using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreListVM
    {
        public PaginationRes<global::Store> PanigationResponse { get; set; }
        public string? Search { get; set; }
        public bool? IsActive { get; set; }

        public StoreListVM(PaginationRes<global::Store> panigationResponse, StoreSearch searchObject)
        {
            PanigationResponse = panigationResponse;
            Search = searchObject.Search;
            IsActive = searchObject.IsActive;
        }
    }
}
