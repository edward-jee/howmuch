using System.Net;
using System.Text;
using howmuch.Core;
using howmuch.Services;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}

var boundary = ImportCalculator.Calculate(150, 20, 1400, 1, 8, false, true);
Check(boundary.IsExempt && boundary.TaxesKrw == 0 && boundary.TotalKrw == 238000, "Inclusive $150 exemption; shipping still paid");
var taxable = ImportCalculator.Calculate(151, 20, 1400, 1, 8, false, true);
Check(!taxable.IsExempt && taxable.DutyKrw == 19152 && taxable.VatKrw == 25855 && taxable.TotalKrw == 284407, "Tax entire value including freight and VAT on duty");
Check(ImportCalculator.Calculate(200, 0, 1400, 1, 8, true, true).IsExempt, "Inclusive US $200 threshold");
Check(!ImportCalculator.Calculate(200.01m, 0, 1400, 1, 8, true, true).IsExempt, "US threshold exceeded");
Check(!ImportCalculator.Calculate(151, 0, 1400, 1, 8, false, true).IsExempt, "USD alone does not imply US shipment");
Check(ImportCalculator.Calculate(10, 0, 1400, 1, 0, false, false).VatKrw == 1400, "No exemption and zero duty still incurs VAT");
Check(ImportCalculator.Calculate(0, 0, 10, .01m, 8, false, true).TotalKrw == 0, "Zero price");
Check(ImportCalculator.TryAmount("12,345.67", out var amount) && amount == 12345.67m, "Grouped decimal parsing");
foreach (var invalid in new[] { "", "-1", "NaN", "1,2", "1e6", "1.23456", "1000000001", "1 234", "." })
    Check(!ImportCalculator.TryAmount(invalid, out _), $"Reject {invalid}");

string directory = Path.Combine(Path.GetTempPath(), "HowMuch-checks-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var store = new LocalStore(directory);
    Check(store.Read<Preferences>("settings.json") is null, "Missing settings");
    Check(store.Write("settings.json", new Preferences("EUR")) && store.Read<Preferences>("settings.json")?.Currency == "EUR", "Settings roundtrip");
    File.WriteAllText(Path.Combine(directory, "settings.json"), "bad json");
    Check(store.Read<Preferences>("settings.json") is null, "Malformed settings tolerated");
    string date = DateTime.UtcNow.ToString("yyyy-MM-dd");
    using var client = new HttpClient(new FakeHandler($$"""
        [{"date":"{{date}}","base":"JPY","quote":"KRW","rate":9.5},
         {"date":"{{date}}","base":"JPY","quote":"USD","rate":0.0067}]
        """));
    var service = new ExchangeRateService(client, store);
    var live = await service.GetAsync("JPY", default);
    Check(!live.IsCached && live.Rates.Krw == 9.5m && live.Rates.Usd == .0067m, "Live response and saved cache");
    using var offlineClient = new HttpClient(new FakeHandler(null));
    var offline = new ExchangeRateService(offlineClient, store);
    Check((await offline.GetAsync("JPY", default)).IsCached, "Network failure explicitly uses cache");
    try { await offline.GetAsync("EUR", default); Check(false, "Missing cache should fail"); }
    catch (HttpRequestException) { checks++; }
    using var malformedClient = new HttpClient(new FakeHandler("[]"));
    Check((await new ExchangeRateService(malformedClient, store).GetAsync("JPY", default)).IsCached, "Incomplete API data falls back to cache");
    store.Write("rates-JPY.json", live.Rates with { Currency = "EUR" });
    Check(offline.ReadCached("JPY") is null, "Reject wrong currency cache");
    store.Write("rates-JPY.json", live.Rates with { Krw = -1 });
    Check(offline.ReadCached("JPY") is null, "Reject invalid rate cache");
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    try { await offline.GetAsync("JPY", cancelled.Token); Check(false, "Cancellation must propagate"); }
    catch (OperationCanceledException) { checks++; }
}
finally { Directory.Delete(directory, true); }
Console.WriteLine($"Passed {checks} checks.");

sealed class FakeHandler(string? response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (response is null) throw new HttpRequestException("Offline");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(response, Encoding.UTF8, "application/json") });
    }
}
