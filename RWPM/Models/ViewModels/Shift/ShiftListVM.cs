using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftListVM
    {
        public PaginationRes<Entities.Shift> PanigationResponse { get; set; }
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsTemplate { get; set; }
        public int? StoreId { get; set; }
        public SelectList? StoreSelectList { get; set; }

        public ShiftListVM(PaginationRes<Entities.Shift> panigationResponse, ShiftSearch searchObject, SelectList? storeSelectList = null)
        {
            PanigationResponse = panigationResponse;
            Search = searchObject.Search;
            IsActive = searchObject.IsActive;
            IsTemplate = searchObject.IsTemplate;
            StoreId = searchObject.StoreId;
            StoreSelectList = storeSelectList;
        }
    }
}
