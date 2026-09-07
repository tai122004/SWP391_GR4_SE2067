using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;

namespace RWPM.Models.Entities
{
    public class ImportLog
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        [Precision(0)]
        public DateTime ImportStartDate { get; set; }
        [Precision(0)]
        public DateTime? ImportDoneDate { get; set; }
        public int TotalRows { get; set; }
        public int SuccessfulRows { get; set; }
        public string ErrorMessages { get; set; } = string.Empty;
        public ImportLogDataType DataType { get; set; }

        public Acc Creator { get; set; } = default!;
    }
}
