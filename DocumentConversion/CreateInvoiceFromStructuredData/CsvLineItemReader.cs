using System.Globalization;
using System.Text;

namespace CreateInvoiceFromStructuredData;

internal static class CsvLineItemReader
{
    public static IReadOnlyList<InvoiceLineItem> Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Line item CSV file was not found.", path);
        }

        List<string[]> rows = File
            .ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseRow)
            .ToList();

        if (rows.Count < 2)
        {
            throw new InvalidDataException("Line item CSV must include a header row and at least one line item.");
        }

        string[] header = rows[0];
        Dictionary<string, int> columns = header
            .Select((name, index) => new { Name = name.Trim(), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        return rows
            .Skip(1)
            .Select((row, index) => CreateLineItem(row, columns, index + 2))
            .ToList();
    }

    public static string[] ParseRow(string row)
    {
        List<string> values = new();
        StringBuilder current = new();
        bool inQuotes = false;

        for (int i = 0; i < row.Length; i++)
        {
            char c = row[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < row.Length && row[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private static InvoiceLineItem CreateLineItem(string[] row, IReadOnlyDictionary<string, int> columns, int rowNumber)
    {
        return new InvoiceLineItem
        {
            ItemCode = Get(row, columns, "itemCode", rowNumber),
            Description = Get(row, columns, "description", rowNumber),
            Quantity = ParseDecimal(Get(row, columns, "quantity", rowNumber), "quantity", rowNumber),
            UnitPrice = ParseDecimal(Get(row, columns, "unitPrice", rowNumber), "unitPrice", rowNumber)
        };
    }

    private static string Get(string[] row, IReadOnlyDictionary<string, int> columns, string columnName, int rowNumber)
    {
        if (!columns.TryGetValue(columnName, out int index))
        {
            throw new InvalidDataException($"Line item CSV is missing required column: {columnName}.");
        }

        if (index >= row.Length)
        {
            throw new InvalidDataException($"Line item CSV row {rowNumber} is missing a value for {columnName}.");
        }

        return row[index].Trim();
    }

    private static decimal ParseDecimal(string value, string columnName, int rowNumber)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result))
        {
            throw new InvalidDataException($"Line item CSV row {rowNumber} has an invalid {columnName} value: {value}");
        }

        return result;
    }
}
