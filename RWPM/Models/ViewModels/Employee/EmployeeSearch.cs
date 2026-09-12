using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeSearch : PagedReq
    {
        private string? _status = "active";

        public string? Search { get; set; }
        public int? StoreId { get; set; }

        public string? Status
        {
            get => _status;
            set => _status = value;
        }

        public bool? IsActive
        {
            get => _status switch
            {
                "active" => true,
                "inactive" => false,
                _ => null
            };
            set
            {
                if (value.HasValue)
                {
                    _status = value.Value ? "active" : "inactive";
                }
                else
                {
                    _status = "all";
                }
            }
        }
    }
}