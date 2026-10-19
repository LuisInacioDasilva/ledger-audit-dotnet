using System.Text.RegularExpressions;

namespace LedgerAudit.Api;

public sealed record LedgerLine(string Account, long AmountMinor);
public sealed record LedgerEntry(string Id, DateTimeOffset OccurredAt, string Description, IReadOnlyList<LedgerLine> Lines);

public static partial class LedgerRules
{
    [GeneratedRegex("^[A-Za-z0-9:_-]{3,80}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z0-9:_-]{2,80}$")]
    private static partial Regex AccountPattern();

    public static string? Validate(LedgerEntry? entry)
    {
        if (entry is null) return "Lançamento ausente";
        if (entry.Id is null || !IdPattern().IsMatch(entry.Id)) return "ID inválido";
        if (entry.Description is null || entry.Description.Trim().Length is < 4 or > 240) return "Descrição inválida";
        if (entry.OccurredAt > DateTimeOffset.UtcNow.AddMinutes(5)) return "Data futura";
        if (entry.Lines is null || entry.Lines.Count < 2 || entry.Lines.Count > 100) return "São necessárias de 2 a 100 linhas";
        if (entry.Lines.Any(line => line is null || line.Account is null || !AccountPattern().IsMatch(line.Account) || line.AmountMinor == 0)) return "Linha inválida";
        try { if (entry.Lines.Aggregate(0L, (sum, line) => checked(sum + line.AmountMinor)) != 0) return "Débitos e créditos não fecham"; }
        catch (OverflowException) { return "Valor acima do limite"; }
        return null;
    }
}
