using System.Text;
using System.Text.Json;

namespace LedgerAudit.Api;

public enum PostResult { Created, Duplicate, Conflict }

public sealed class LedgerStore
{
    private readonly object gate = new();
    private readonly string path;
    private readonly Dictionary<string, LedgerEntry> entries = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public LedgerStore(string path)
    {
        this.path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (!File.Exists(path)) return;
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var entry = JsonSerializer.Deserialize<LedgerEntry>(line, JsonOptions) ?? throw new InvalidDataException($"Linha {lineNumber} inválida");
            if (LedgerRules.Validate(entry) is { } reason) throw new InvalidDataException($"Linha {lineNumber}: {reason}");
            if (!entries.TryAdd(entry.Id, entry)) throw new InvalidDataException($"ID duplicado na linha {lineNumber}");
        }
    }

    public PostResult Post(LedgerEntry entry)
    {
        if (LedgerRules.Validate(entry) is { } reason) throw new ArgumentException(reason, nameof(entry));
        lock (gate)
        {
            if (entries.TryGetValue(entry.Id, out var existing))
                return JsonSerializer.Serialize(existing, JsonOptions) == JsonSerializer.Serialize(entry, JsonOptions) ? PostResult.Duplicate : PostResult.Conflict;
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry, JsonOptions) + "\n");
            using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            file.Write(bytes); file.Flush(true);
            entries.Add(entry.Id, entry);
            return PostResult.Created;
        }
    }

    public IReadOnlyList<LedgerEntry> List()
    {
        lock (gate) return entries.Values.OrderByDescending(entry => entry.OccurredAt).ThenBy(entry => entry.Id).ToList();
    }

    public LedgerEntry? Find(string id)
    {
        lock (gate) return entries.GetValueOrDefault(id);
    }

    public IReadOnlyDictionary<string, long> Balances()
    {
        lock (gate)
        {
            var balances = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var entry in entries.Values)
                foreach (var line in entry.Lines)
                    balances[line.Account] = checked(balances.GetValueOrDefault(line.Account) + line.AmountMinor);
            return balances;
        }
    }
}
