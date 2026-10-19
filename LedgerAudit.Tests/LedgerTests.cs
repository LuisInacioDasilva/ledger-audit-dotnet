using LedgerAudit.Api;
using Xunit;

namespace LedgerAudit.Tests;

public sealed class LedgerTests
{
    private static LedgerEntry Entry(string id = "event-001", long value = 12500) =>
        new(id, DateTimeOffset.Parse("2026-10-19T12:00:00Z"), "Liquidação do pedido 42",
            [new("recebiveis", value), new("receita", -value)]);

    [Fact]
    public void BalancedEntryIsAccepted() => Assert.Null(LedgerRules.Validate(Entry()));

    [Fact]
    public void UnbalancedEntryIsRejected()
    {
        var invalid = Entry() with { Lines = [new("recebiveis", 12500), new("receita", -12499)] };
        Assert.Equal("Débitos e créditos não fecham", LedgerRules.Validate(invalid));
    }

    [Fact]
    public void OverflowIsRejected()
    {
        var invalid = Entry() with { Lines = [new("a1", long.MaxValue), new("a2", 1), new("a3", -long.MaxValue), new("a4", -1)] };
        Assert.Equal("Valor acima do limite", LedgerRules.Validate(invalid));
    }

    [Fact]
    public void ReplayPreservesBalancesAndIdempotency()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledger-{Guid.NewGuid():N}.ndjson");
        try
        {
            var first = new LedgerStore(path);
            Assert.Equal(PostResult.Created, first.Post(Entry()));
            Assert.Equal(PostResult.Duplicate, first.Post(Entry()));
            Assert.Equal(PostResult.Conflict, first.Post(Entry(value: 13000)));
            var replay = new LedgerStore(path);
            Assert.Single(replay.List());
            Assert.Equal(12500, replay.Balances()["recebiveis"]);
            Assert.Equal(-12500, replay.Balances()["receita"]);
            Assert.NotNull(replay.Find("event-001"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ConcurrentDuplicatesOnlyAppendOnce()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledger-{Guid.NewGuid():N}.ndjson");
        try
        {
            var store = new LedgerStore(path);
            var results = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => Task.Run(() => store.Post(Entry()))));
            Assert.Single(results.Where(result => result == PostResult.Created));
            Assert.Equal(23, results.Count(result => result == PostResult.Duplicate));
            Assert.Single(File.ReadAllLines(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CorruptedReplayFailsClosed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledger-{Guid.NewGuid():N}.ndjson");
        try
        {
            File.WriteAllText(path, "{not-json}\n");
            Assert.ThrowsAny<Exception>(() => new LedgerStore(path));
        }
        finally { File.Delete(path); }
    }
}
