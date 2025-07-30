using ClosedXML.Excel;

namespace BdlGusExporter.Core
{
    // A simple record to hold the necessary info for exporting
    public record ExportDataRow(string VariableName, string UnitName, int Year, decimal? Value);

    public class ExcelExportService
    {
        public XLWorkbook CreateWorkbook(Dictionary<string, List<ExportDataRow>> dataByVariable)
        {
            var wb = new XLWorkbook();

            foreach (var pair in dataByVariable)
            {
                var varId = pair.Key;
                var dataRows = pair.Value;
                var varName = dataRows.FirstOrDefault()?.VariableName ?? $"Var_{varId}";

                var ws = wb.Worksheets.Add($"Var_{varId}");
                ws.Cell(1, 1).Value = "WSKAŹNIK";
                ws.Cell(1, 2).Value = "JEDNOSTKA";
                ws.Cell(1, 3).Value = "ROK";
                ws.Cell(1, 4).Value = "WARTOŚĆ";

                int row = 2;
                foreach (var dataRow in dataRows.OrderBy(r => r.UnitName).ThenBy(r => r.Year))
                {
                    ws.Cell(row, 1).Value = dataRow.VariableName;
                    ws.Cell(row, 2).Value = dataRow.UnitName;
                    ws.Cell(row, 3).Value = dataRow.Year;
                    var cell = ws.Cell(row, 4);
                    if (dataRow.Value.HasValue)
                        cell.SetValue(dataRow.Value.Value);
                    else
                        cell.SetValue("n/d");
                    row++;
                }
            }

            return wb;
        }
    }
}
