namespace Ledger.Core;

public interface ICategorizationRule
{
    Category? Match(Transaction tx);
}


public sealed class KeywordRule (
    string keyword,
    Category category
) : ICategorizationRule
{
    private readonly string _keyword = !string.IsNullOrWhiteSpace(keyword) 
        ? keyword:
        throw new ArgumentException("provided an empty a null keyword value");
    private readonly Category _category = category;

    public Category? Match(Transaction tx)
    {
       return tx.Description
                .Contains(_keyword, StringComparison.OrdinalIgnoreCase) ? _category : null;
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