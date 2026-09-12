using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Models;
using RWPM.Models.Entities;

namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeListVM
    {
        public PaginationRes<RWPM.Models.Entities.Employee> PanigationResponse { get; set; }
        public string? Search { get; set; }
        public int? StoreId { get; set; }
        public string? Status { get; set; }
        public bool? IsActive { get; set; }
        public SelectList? StoreSelectList { get; set; }

        public EmployeeListVM(PaginationRes<RWPM.Models.Entities.Employee> panigationResponse, EmployeeSearch searchObject, SelectList? storeSelectList = null)
        {
            PanigationResponse = panigationResponse;
            Search = searchObject.Search;
            StoreId = searchObject.StoreId;
            Status = searchObject.Status;
            IsActive = searchObject.IsActive;
            StoreSelectList = storeSelectList;
        }
    }
}