using System.Security.Cryptography;
using System.Text;
using LedgerAudit.Api;

var key = Environment.GetEnvironmentVariable("LEDGER_API_KEY") ?? "";
if (key.Length < 24) throw new InvalidOperationException("Defina LEDGER_API_KEY com pelo menos 24 caracteres");
var file = Environment.GetEnvironmentVariable("LEDGER_DATA_FILE") ?? "./data/entries.ndjson";
var store = new LedgerStore(file);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
builder.Services.AddSingleton(store);
var app = builder.Build();

app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'";
    await next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", (LedgerStore ledger) => Results.Ok(new { status = "ok", entries = ledger.List().Count }));
app.MapGet("/api/entries", (LedgerStore ledger) => Results.Ok(ledger.List()));
app.MapGet("/api/entries/{id}", (string id, LedgerStore ledger) => ledger.Find(id) is { } entry ? Results.Ok(entry) : Results.NotFound());
app.MapGet("/api/balances", (LedgerStore ledger) => Results.Ok(ledger.Balances()));
app.MapPost("/api/entries", (HttpRequest request, LedgerEntry entry, LedgerStore ledger) => {
    if (!Authorized(request.Headers.Authorization.ToString(), key)) return Results.Unauthorized();
    if (LedgerRules.Validate(entry) is { } reason) return Results.UnprocessableEntity(new { error = reason });
    try {
        return ledger.Post(entry) switch {
            PostResult.Created => Results.Created($"/api/entries/{entry.Id}", new { entry.Id, duplicate = false }),
            PostResult.Duplicate => Results.Ok(new { entry.Id, duplicate = true }),
            _ => Results.Conflict(new { error = "ID já usado para outro lançamento" })
        };
    } catch (IOException) { return Results.Problem("Armazenamento indisponível", statusCode: 503); }
});

app.Run();

static bool Authorized(string header, string key)
{
    if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return false;
    var actual = SHA256.HashData(Encoding.UTF8.GetBytes(header[7..]));
    var expected = SHA256.HashData(Encoding.UTF8.GetBytes(key));
    return CryptographicOperations.FixedTimeEquals(actual, expected);
}

public partial class Program { }
