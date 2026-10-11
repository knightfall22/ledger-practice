using System.Text.Json;
using Ledger.Core;

(string? csvPath, string? rulePath) = args switch
{
    ["import",  var p] => (p, null),
    ["import", var p, "--rules", var r] => (p, r),
    _ => (null, null)
};

if (csvPath is null)
{   
    Console.Error.WriteLine("usage: ledger import <file.csv> [--rules rules.csv]");
    return 1;
}

IReadOnlyList<ICategorizationRule> rules = [];
if (rulePath is not null)
{
    try
    {
        rules = await RuleLoader.LoadAsync(rulePath);
    }
    catch (Exception ex) when (ex is FormatException or JsonException or FileNotFoundException)
    {
        Console.Error.WriteLine($"invalid file rule {ex.Message}");
        return 2;
    }
}

var categorizer = new Categorizer(rules);
var result = await CsvImporter.ImportAsync(csvPath);

foreach (var tx in result.Transactions.Select(categorizer.Categorize))
{
    var category = tx.Category?.ToString() ?? "-";
    Console.WriteLine($"{tx.Date:yyyy-MM-dd}  {tx.Description,-20} {tx.Amount,10:N2} {tx.Currency}  {category}");
}

if (result.BadLines.Count > 0)
    Console.Error.WriteLine($"skipped lines: {string.Join(", ", result.BadLines)}");
return 0;