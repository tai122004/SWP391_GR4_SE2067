namespace RWPM.Common.Models
{
    public class PagedReq
    {
        private int _pageSize = 50;
        private const int MaxPageSize = 50;

        public int PageNumber { get; set; } = 1;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
        }
    }
}
