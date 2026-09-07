namespace RWPM.Common.Models
{
    public class ImportDataResult
    {
        public readonly int TotalRows;
        public int ProcessedRows { get; set; }
        public List<ImportDataFailedResult> FailedData { get; set; } = new();

        public ImportDataResult(int totalRows)
        {
            TotalRows = totalRows;
        }
    }

    public class ImportDataFailedResult
    {
        public int RowNumber { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public ImportDataFailedResult(int rowNumber, string reason, string value)
        {
            RowNumber = rowNumber;
            Reason = reason;
            Value = value;
        }
    }
}
