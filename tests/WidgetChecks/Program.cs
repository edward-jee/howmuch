using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using howmuch;
using howmuch.Services;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HowMuch-widget-" + Guid.NewGuid());
        var store = new LocalStore(directory);
        var window = new MainWindow(store, new HttpClient(new DelayedRates()));
        int exitCode = 0;
        window.Loaded += async (_, _) =>
        {
            try
            {
                await Task.Delay(300);
                window.UpdateLayout();
                var currency = (ComboBox)window.FindName("CurrencyInput");
                Require((string)currency.SelectedItem == "JPY", "Default JPY");
                Require(window.Topmost && window.ResizeMode == ResizeMode.NoResize, "Topmost and fixed size");
                var price = (TextBox)window.FindName("PriceInput");
                price.Text = "10000";
                Require(((TextBlock)window.FindName("PriceResult")).Text == "₩95,000", "Immediate KRW conversion");
                price.Text = "bad";
                Require(((TextBlock)window.FindName("TotalResult")).Text == "—", "Invalid input clears results");
                price.Text = "10000";
                currency.SelectedItem = "EUR";
                currency.SelectedItem = "USD";
                await Task.Delay(300);
                Require(((TextBlock)window.FindName("RateStatus")).Text.Contains("1 USD"), "Latest currency wins request race");
                Require(store.Read<Preferences>("settings.json")?.Currency == "USD", "Currency saved immediately");
                foreach (var element in Descendants(window).OfType<FrameworkElement>()
                    .Where(e => e.IsVisible && e.ActualHeight > 0 && e is TextBlock or TextBox or ComboBox or Button or CheckBox))
                {
                    var location = element.TransformToAncestor(window).Transform(new Point());
                    Require(location.Y + element.ActualHeight <= window.ActualHeight + 1, $"Layout fits: {element.GetType().Name} {element.Name} ends at {location.Y + element.ActualHeight:0.0}");
                }
                Console.WriteLine("Passed native WPF initialization, layout, input, persistence, and request-race checks.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); exitCode = 1; }
            finally { window.Close(); }
        };
        new Application().Run(window);
        if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
        return exitCode;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private sealed class DelayedRates : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string currency = request.RequestUri!.Query.Split("base=")[1].Split('&')[0];
            // EUR deliberately completes after USD to exercise cancellation and currency guarding.
            await Task.Delay(currency == "EUR" ? 200 : 30, token);
            string date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string krw = currency == "JPY" ? "9.5" : "1400";
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""
                    [{"date":"{{date}}","base":"{{currency}}","quote":"KRW","rate":{{krw}}},
                     {"date":"{{date}}","base":"{{currency}}","quote":"USD","rate":0.0067}]
                    """)
            };
        }
    }
}
