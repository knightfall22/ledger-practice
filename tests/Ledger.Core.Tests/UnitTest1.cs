using System.Text.Json;

namespace Ledger.Core.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var tx1 = new Transaction(
            new DateOnly(2015, 10, 21),
            "Bought a new car",
            20.10m,
            Currency.RON
        );

        var tx2 = new Transaction(
            new DateOnly(2021, 12, 10),
            "Perform some extra-judiciary activites",
            20000m,
            Currency.RON
        );

        var tx3 = tx2;

        var tx4 = new Transaction(
            new DateOnly(2021, 12, 10),
            "Perform some extra-judiciary activites",
            20000m,
            Currency.RON
        );

        var tx5 = tx4 with
        {
            Description = "I am in love with coffee",
        };

        Console.WriteLine(tx4 == tx3);
        Console.WriteLine(ReferenceEquals(tx2, tx3));
        Console.WriteLine(tx4 == tx5);
    }

    // [Fact]
    // public void Parses_valid_line()
    // {
    //     var (tx, error) = TransactionParser.Parse("2026-05-20,coffee shop,30,RON");

    //     Assert.Null(error);
    //     Assert.NotNull(tx);
    //     Assert.Equal(new DateOnly(2026, 5, 20), tx.Date);
    //     Assert.Equal(30m, tx.Amount);
    //     Assert.Equal(Currency.RON, tx.Currency);
    // }

    [Fact]
    public void TryParse_valid_line()
    {
        var line = "2026-05-20,coffee shop,30,RON";
        if (TransactionParser.TryParse(line, out var tx))
        {
            Assert.NotNull(tx);
            Assert.Equal(new DateOnly(2026, 5, 20), tx.Date);
            Assert.Equal(30m, tx.Amount);
            Assert.Equal(Currency.RON, tx.Currency);
        }
    }

    [Fact]
    public void ParseLine_valid_line()
    {
        try
        {
            var line = "2026-05-20,coffee shop,30,RON";
            var tx = TransactionParser.Parse(line);
            
            Assert.NotNull(tx);
            Assert.Equal(new DateOnly(2026, 5, 20), tx.Date);
            Assert.Equal(30m, tx.Amount);
            Assert.Equal(Currency.RON, tx.Currency);
        }
        catch (FormatException e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    [Theory]
    [InlineData("2026-05-20,coffee shop30,RON", "not enough arguments")]
    [InlineData("20-05-2026,coffee,30,RON", "cannot parse date")]
    [InlineData("2026-05-20,coffee,abc,RON", "cannot parse number")]
    [InlineData("2026-05-20,coffee,30,GBP", "cannot parse currency")]
    public void Parse_invalid_line(string line, string expectedMessage)
    {
        var ex = Assert.Throws<FormatException>(() => TransactionParser.Parse(line));
        Assert.Contains(expectedMessage, ex.ToString());
    }




    public sealed class CsvImporterTests : IDisposable
    {
        private readonly string _path = Path.GetTempFileName();

        public void Dispose() => File.Delete(_path);

        [Fact]
        public async Task Test_valid_CsvImporter()
        {
            var tmpFile = _path;

            string[] line =
            [
                "date,description,amount,currency",
            "2026-10-01,Coffee Shop,-4.50,EUR",
            "2026-10-01,Salary,3200.00,ron",
        ];

            File.WriteAllLines(tmpFile, line);

            var (txs, badlines) = await CsvImporter.ImportAsync(tmpFile);
            Assert.Equal(2, txs.Count);
            Assert.Empty(badlines);
        }

        [Fact]
        public async Task Test_invalid_CsvImporter()
        {
            var tmpFile = _path;

            string[] badlines =
            [
            "date,description,amount,currency",
            "invalid-date,Coffee Shop,-4.50,EUR",
            "2026-10-01,Salary,NAN,RON",
        ];

            File.WriteAllLines(tmpFile, badlines);

            var (txs, lines) = await CsvImporter.ImportAsync(tmpFile);
            Assert.Empty(txs);
            Assert.Equal(2, lines.Count);
            Assert.Equal([2,3], lines);
        }
    }


    public static TheoryData<string, string, Category, Category?> RuleCases => new()
{
    { "2026-05-20,coffee shop,30,RON",  "coffee", Category.Food, Category.Food },
    { "2026-05-20,COFFEE SHOP,30,RON",  "coffee", Category.Food, Category.Food },
    { "2026-05-20,gas station,200,RON", "coffee", Category.Food, null },
};

    [Theory]
    [MemberData(nameof(RuleCases))]
    public void Match_ReturnsExpectedCategory(
        string line, string keyword, Category ruleCategory, Category? expected)
    {
        var tx = TransactionParser.Parse(line);
        var rule = new KeywordRule(keyword, ruleCategory);

        Assert.Equal(expected, rule.Match(tx));
    }


    [Fact]
    public void TestRuleCategorization()
    {
        List<ICategorizationRule> rules = [
           new AmountRangeRule(-50, 25, Category.Food),
           new KeywordRule("Metro", Category.Travel)
        ];

        var tx = new Transaction(new DateOnly(2026, 10, 10), "Metro card", 20, Currency.USD);

        var categorizer = new Categorizer(rules);
        var match = categorizer.Categorize(tx); 

        Assert.Equal(match.Category, Category.Food);
    }

    [Fact]
    public void TestAllofRuleCategorization()
    {
        ICategorizationRule[] rules = [
           new AmountRangeRule(-50, 25, Category.Food),
           new KeywordRule("Metro", Category.Travel)
        ];

        var tx = new Transaction(new DateOnly(2026, 10, 10), "Metro card", 20, Currency.USD);

        var categorizer = new AllOfRule(Category.Travel, rules);
        var match = categorizer.Match(tx); 

        Assert.Equal(match, Category.Travel);
    }


    public sealed class RuleDeserializingTests : IDisposable
    {
        private readonly string _path = Path.GetTempFileName();

        public void Dispose() => File.Delete(_path);

        private void WriteValidData()
        {
            string data = """
                [
                    { "type": "keyword", "keyword": "Coffee", "category": "Food" },
                    { "type": "keyword", "keyword": "Supermarket", "category": "Food" },
                    { "type": "keyword", "keyword": "Bistro", "category": "Food" },
                    { "type": "keyword", "keyword": "Cinema", "category": "Entertainment" },
                    { "type": "keyword", "keyword": "Wizz Air", "category": "Travel" },
                    { "type": "keyword", "keyword": "Metro", "category": "Travel" }
                ]
            """;

            File.WriteAllText(_path, data);
        }

        private void WriteInvalidRuleData()
        {
            string data = """
                [
                    { "type": "", "keyword": "Coffee", "category": "Food" },
                    { "type": "micheal", "keyword": "Supermarket", "category": "Food" },
                    { "type": "keyword", "keyword": "Bistro", "category": "Food" },
                    { "type": "keyword", "keyword": "Cinema", "category": "Entertainment" },
                    { "type": "keyword", "keyword": "Wizz Air", "category": "Travel" },
                    { "type": "keyword", "keyword": "Metro", "category": "Travel" }
                ]
            """;

            File.WriteAllText(_path, data);
        }

        private void WriteInvalidJson()
        {
            

            File.WriteAllText(_path, "null");
        }

        [Fact]
        public async Task Test_Rule_Deserializer()
        {
            try
            {
                WriteValidData();
                var loader = await RuleLoader.LoadAsync(_path);

                Assert.NotEmpty(loader);
                var fisrtRule = loader![0];
                Assert.IsType<KeywordRule>(fisrtRule);
                
            }
            catch (FormatException e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        [Fact]
        public async Task Test_Invalid_rule_category()
        {
            try
            {
                WriteInvalidRuleData();
                await RuleLoader.LoadAsync(_path);
            }
            catch (FormatException e)
            {
                Assert.Contains("invalid rule", e.ToString());
            }
        }

        [Fact]
        public async Task Test_Invalid_json()
        {
            try
            {
                WriteInvalidJson();
                await RuleLoader.LoadAsync(_path);
            }
            catch (FormatException e)
            {
                Assert.Contains("rule file contains null", e.ToString());
            }
        }
    }
}


