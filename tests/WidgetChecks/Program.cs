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
        store.Write("settings.json", new Preferences("CNY"));
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
                Require(currency.Items.Cast<string>().SequenceEqual(new[] { "JPY", "USD", "EUR" }), "Only prioritized source currencies");
                Require(((RadioButton)window.FindName("GeneralGoodsInput")).IsChecked == true, "Default general goods");
                Require(((Expander)window.FindName("AdvancedSettings")).IsExpanded == false, "Optional settings initially hidden");
                Require(window.Title == "얼마일까 · 해외직구 계산기", "Korean window title");
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
                price.Text = "175";
                ((CheckBox)window.FindName("UsInput")).IsChecked = true;
                Require(((TextBlock)window.FindName("DutyResult")).Text == "₩0", "US general goods exempt at $175");
                Require(((TextBlock)window.FindName("ThresholdText")).Text.Contains("200"), "Show US general threshold");
                ((RadioButton)window.FindName("ExcludedGoodsInput")).IsChecked = true;
                Require(((TextBlock)window.FindName("DutyResult")).Text == "₩19,600", "US excluded goods taxed at $175");
                Require(((TextBlock)window.FindName("VatResult")).Text == "₩26,460", "VAT includes duty");
                Require(((TextBlock)window.FindName("ThresholdText")).Text.Contains("150"), "Show excluded threshold");
                price.Text = "150";
                Require(((TextBlock)window.FindName("VatResult")).Text == "₩0", "Excluded goods still exempt at $150");
                price.Text = "175";
                var advanced = (Expander)window.FindName("AdvancedSettings");
                advanced.IsExpanded = true;
                ((TextBox)window.FindName("ShippingInput")).Text = "10";
                ((TextBox)window.FindName("DutyInput")).Text = "13";
                Require(((TextBlock)window.FindName("TotalResult")).Text == "₩321,937", "Optional shipping/duty update full taxable total");
                advanced.IsExpanded = false;
                window.UpdateLayout();
                foreach (var element in Descendants(window).OfType<FrameworkElement>()
                    .Where(e => e.IsVisible && e.ActualHeight > 0 && e is TextBlock or TextBox or ComboBox or Button or CheckBox or RadioButton))
                {
                    var location = element.TransformToAncestor(window).Transform(new Point());
                    Require(location.Y + element.ActualHeight <= window.ActualHeight + 1, $"Layout fits: {element.GetType().Name} {element.Name} ends at {location.Y + element.ActualHeight:0.0}");
                    Require(location.X >= -1 && location.X + element.ActualWidth <= window.ActualWidth + 1, $"Horizontal layout fits: {element.GetType().Name} {element.Name}");
                }
                Console.WriteLine("Passed native Korean WPF layout, currency priority, customs options, optional settings, persistence, and request-race checks.");
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
