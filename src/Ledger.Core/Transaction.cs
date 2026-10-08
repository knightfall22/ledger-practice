using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Ledger.Core;

public enum Currency { EUR, USD, RON }

public enum Category { Food, Entertainment, Travel, Other }

public sealed record Transaction (
    DateOnly Date,
    string Description,
    decimal Amount,
    Currency Currency
)
{
    public Category? Category{get; init;}
    public bool IsExpense => Amount < 0;
}


public static class TransactionParser
{
    private static (Transaction? Tx, string? Error) ParseWithError(string line)
    {
        var parts = line.Split(",");
        if (parts.Length is not (4 or 5)) return (null, "not enough arguments");

        if (!DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                       DateTimeStyles.None, out var date))
        {
            return (null, "cannot parse date");
        }

        if (!decimal.TryParse(
            parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount
        ))
        {
            return (null, "cannot parse number");
        }

        if (!Enum.TryParse<Currency>(parts[3], ignoreCase: true, out var currency) 
            || !Enum.IsDefined(currency)
        )
        {

            return (null, "cannot parse currency");
        }

        // Category? category = null;
        // if( parts.Length == 5 && !string.IsNullOrWhiteSpace(parts[4]))
        // {
        //     if (!Enum.TryParse<Category>(parts[4], ignoreCase: true, out var parsed) 
        //         || !Enum.IsDefined(parsed)
        //     )
        //     {
        //         return (null, "cannot parse category");
        //     }
        //     category = parsed;
        // }


        return (new Transaction(date, parts[1], amount, currency), null);
    }

    public static bool TryParse(string line, [NotNullWhen(true)] out Transaction? tx)
    {
        (tx, _) = ParseWithError(line);
       return tx is not null;
    }

    public static Transaction Parse(string line)
    {
        var (tx, error) = ParseWithError(line);
        return tx ?? throw new FormatException(error);
    }
}


public sealed record ImportResult(IReadOnlyList<Transaction> Transactions, IReadOnlyList<int> BadLines);

public static class CsvImporter
{
    public static async Task<ImportResult> ImportAsync(string path, CancellationToken ct = default)
    {
        var txs = new List<Transaction>();
        var badtxs = new List<int>();
        var lineNo = 0;

        await foreach (var line in File.ReadLinesAsync(path, ct))
        {
            lineNo++;
            if (lineNo==1) continue;
            if (TransactionParser.TryParse(line, out var tx)) txs.Add(tx); else
            {
                badtxs.Add(lineNo);
            }
            
        }

        return new ImportResult(txs, badtxs);
    }


   
}