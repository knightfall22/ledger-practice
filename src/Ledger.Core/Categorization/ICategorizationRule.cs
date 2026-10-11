using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ledger.Core;

public interface ICategorizationRule
{
    Category? Match(Transaction tx);
}


// public sealed class KeywordRule (
//     string keyword,
//     Category category
// ) : ICategorizationRule
// {
//     private readonly string _keyword = !string.IsNullOrWhiteSpace(keyword) 
//         ? keyword:
//         throw new ArgumentException("provided an empty a null keyword value");
//     private readonly Category _category = category;

//     public Category? Match(Transaction tx)
//     {
//        return tx.Description
//                 .Contains(_keyword, StringComparison.OrdinalIgnoreCase) ? _category : null;
//     }
// }

public abstract class CategorizationRule(Category category): ICategorizationRule
{
    public Category Category{get;} = category;

    public Category? Match(Transaction tx) => IsMatch(tx) ? Category: null;

    protected abstract bool IsMatch(Transaction tx);
}

public sealed class KeywordRule(
    string keyword,
    Category category
) : CategorizationRule(category)
{
    protected override bool IsMatch(Transaction tx)
    {
        return tx.Description
                    .Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}


public sealed class AmountRangeRule : ICategorizationRule
{
    public decimal Min {get;}
    public decimal Max {get;}
    public Category Category {get;}

    public AmountRangeRule(decimal min, decimal max, Category category)
    {
        if (min > max) throw new ArgumentException($"min ({min}) must not be greater than $({max})", nameof(min));

        Min = min;
        Max = max;
        Category = category;
    }

    public Category? Match(Transaction tx)
    {
        return tx.Amount >= Min && tx.Amount <= Max ? Category : null;
    }

}

public sealed class Categorizer(IReadOnlyList<ICategorizationRule> rules)
{
    public Transaction Categorize(Transaction tx)
    {
        foreach(var rule in rules)
        {
            if (rule.Match(tx) is { } category)
            {
                return tx with { Category = category };
            }
        }
        return tx;
    }
}

public sealed class AllOfRule(Category category, params ICategorizationRule[] conditions): ICategorizationRule
{
    public Category? Match(Transaction tx) =>
        conditions.All(c => c.Match(tx) is not null) ? category: null;
}


public sealed record RuleDto(string? Type, string? Keyword, decimal? Min, decimal? Max, Category? Category);

public static class RuleLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive=true,
        Converters = {new JsonStringEnumConverter() },
    };

    public static async Task<IReadOnlyList<ICategorizationRule>> 
        LoadAsync(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        var dtos = await JsonSerializer.DeserializeAsync<List<RuleDto?>>(stream, Options, ct) ??
         throw new FormatException("rule file contains null");

        return dtos.Select(ToRule).ToList();

    }

    private static ICategorizationRule ToRule(RuleDto? dto) => dto switch {
      {Type: "keyword", Keyword: { } kw, Category: { } cat } => new KeywordRule(kw, cat),
      {Type: "amount", Min: { } min, Max: { } max, Category: { } cat } => new AmountRangeRule(min, max, cat),
      _ => throw new FormatException($"invalid rule: {dto?.ToString() ?? "null"}")
    };
} 