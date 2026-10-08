using Ledger.Core;

if (args is not ["import", var path])
{
    Console.Error.WriteLine("usage: ledger import <file.csv>");
    return 1;
}

if (!File.Exists(path))
{
    Console.Error.WriteLine($"file not found: {path}");
    return 1;
}

var (txs, badlines) = await CsvImporter.ImportAsync(path);

foreach (var tx in txs)
{
    Console.WriteLine($"{tx.Date:yyyy-MM-dd}  {tx.Description,-20} {tx.Amount,10:N2} {tx.Currency}");
};

if (badlines.Count > 0)
{
    Console.Error.Write($"skipped lines: {string.Join(",", badlines)}");
}


return 0;