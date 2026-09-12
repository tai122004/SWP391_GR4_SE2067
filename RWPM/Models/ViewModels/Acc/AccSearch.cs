using RWPM.Common.Models;

namespace RWPM.Models.ViewModels.Acc
{
    public class AccSearch : PagedReq
    {
        public string? Search { get; set; }

        /// <summary>
        /// Receives "true", "false", or "" from query string (name="IsActive")
        /// Used as string to avoid bool? binding issue with empty string
        /// </summary>
        private string? _isActiveStr = null;

        public string? IsActive
        {
            get => _isActiveStr;
            set => _isActiveStr = value;
        }

        /// <summary>
        /// Actual bool? used by service for filtering
        /// </summary>
        public bool? IsActiveBool => _isActiveStr switch
        {
            "true" => true,
            "false" => false,
            _ => null
        };
    }
}