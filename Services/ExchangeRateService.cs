using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace howmuch.Services;

public sealed record ExchangeRates(string Currency, decimal Krw, decimal Usd, DateOnly KrwDate,
    DateOnly UsdDate, DateTimeOffset RetrievedAt)
{
    public bool IsValid(string currency) => Currency == currency && Krw is > 0 and < 1_000_000 &&
        Usd is > 0 and < 1_000_000 && KrwDate != default && UsdDate != default &&
        KrwDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) &&
        UsdDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) &&
        RetrievedAt != default && RetrievedAt <= DateTimeOffset.UtcNow.AddMinutes(5);
}

public sealed record RateResult(ExchangeRates Rates, bool IsCached);

public sealed class ExchangeRateService(HttpClient client, LocalStore store)
{
    public static readonly string[] Currencies = ["JPY", "USD", "EUR", "CNY", "GBP", "AUD", "CAD", "CHF", "HKD", "SGD", "TWD", "THB", "KRW"];

    public ExchangeRates? ReadCached(string currency)
    {
        if (!Currencies.Contains(currency)) return null;
        var cached = store.Read<ExchangeRates>($"rates-{currency}.json");
        return cached?.IsValid(currency) == true ? cached : null;
    }

    public async Task<RateResult> GetAsync(string currency, CancellationToken cancellationToken)
    {
        if (!Currencies.Contains(currency)) throw new ArgumentException("Unsupported currency.", nameof(currency));
        try
        {
            var rows = await client.GetFromJsonAsync<List<RateRow>>(
                $"https://api.frankfurter.dev/v2/rates?base={currency}&quotes=KRW,USD", cancellationToken)
                ?? throw new JsonException("Missing rates.");
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var krw = currency == "KRW" ? new RateRow(today, currency, "KRW", 1) :
                rows.Single(r => r.Base == currency && r.Quote == "KRW");
            var usd = currency == "USD" ? new RateRow(today, currency, "USD", 1) :
                rows.Single(r => r.Base == currency && r.Quote == "USD");
            var rates = new ExchangeRates(currency, krw.Rate, usd.Rate, krw.Date, usd.Date, DateTimeOffset.UtcNow);
            if (!rates.IsValid(currency)) throw new JsonException("Invalid rates.");
            store.Write($"rates-{currency}.json", rates);
            return new(rates, false);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException ||
                                   ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            var cached = ReadCached(currency);
            if (cached is not null) return new(cached, true);
            throw new HttpRequestException("Rates unavailable and no saved rates exist.", ex);
        }
    }

    private sealed record RateRow(
        [property: JsonPropertyName("date")] DateOnly Date,
        [property: JsonPropertyName("base")] string Base,
        [property: JsonPropertyName("quote")] string Quote,
        [property: JsonPropertyName("rate")] decimal Rate);
}
