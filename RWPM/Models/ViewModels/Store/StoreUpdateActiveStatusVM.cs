using RWPM.Common.Attributes;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreUpdateActiveStatusVM
    {
        [RequiredLocalization]
        public int? StoreId { get; set; }

        [RequiredLocalization]
        public bool? IsActive { get; set; }
    }
}
