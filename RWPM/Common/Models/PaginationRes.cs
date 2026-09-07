namespace RWPM.Common.Models
{
    public class PaginationRes<T>
    {
        public IEnumerable<T> Data { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);

        public int StartItem => TotalRecords == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
        public int EndItem => Math.Min(PageNumber * PageSize, TotalRecords);
        public PaginationRes(IEnumerable<T> data, int pageNumber, int pageSize, int totalRecords)
        {
            Data = data;
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalRecords = totalRecords;
        }

        public PaginationRes ToPaginationRes(IDictionary<string, string?>? routeData)
        {
            return new PaginationRes(PageNumber,
                PageSize, 
                TotalRecords,
                TotalPages,
                StartItem,
                EndItem,
                routeData);
        }
    }

    public class PaginationRes
    {
        public readonly int PageNumber;
        public readonly int PageSize;
        public readonly int TotalRecords;
        public readonly int TotalPages;
        public readonly int StartItem;
        public readonly int EndItem;
        public readonly IDictionary<string, string> RouteData = new Dictionary<string, string>();

        public PaginationRes(int pageNumber, int pageSize, int totalRecords, int totalPages, int startItem, int endItem, IDictionary<string, string?>? routeData)
        {
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalRecords = totalRecords;
            TotalPages = totalPages;
            StartItem = startItem;
            EndItem = endItem;

            if (routeData != null)
            {
                foreach (var item in routeData)
                {
                    if(item.Value != null)
                    {
                        RouteData.Add(item.Key, item.Value);
                    }
                }
            }
        }
    }
}
