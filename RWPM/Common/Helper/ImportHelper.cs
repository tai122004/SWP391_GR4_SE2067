using DocumentFormat.OpenXml.Spreadsheet;
using System.Collections.Immutable;

namespace RWPM.Common
{
    public static class ImportHelper
    {
        public static void EnsureColumnNameCorrect(string[] requiredColumns, string[] columnsUsed)
        {
            for (int i = 0; i < requiredColumns.Length; i++)
            {
                if (!string.Equals(requiredColumns[i].Trim(), columnsUsed[i].Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The excel file not right format.");
                }
            }
        }
    }
}
