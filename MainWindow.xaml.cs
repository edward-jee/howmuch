using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using howmuch.Core;
using howmuch.Services;

namespace howmuch;

public partial class MainWindow : Window
{
    private readonly LocalStore store;
    private readonly HttpClient client;
    private readonly ExchangeRateService rateService;
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMinutes(30) };
    private CancellationTokenSource? rateRequest;
    private ExchangeRates? rates;
    private bool ready;
    private bool closing;

    public MainWindow() : this(new LocalStore(), new HttpClient { Timeout = TimeSpan.FromSeconds(12) }) { }

    public MainWindow(LocalStore store, HttpClient client)
    {
        this.store = store;
        this.client = client;
        rateService = new(client, store);
        InitializeComponent();
        var preferences = store.Read<Preferences>("settings.json") ?? new();
        CurrencyInput.ItemsSource = ExchangeRateService.Currencies;
        CurrencyInput.SelectedItem = ExchangeRateService.Currencies.Contains(preferences.Currency) ? preferences.Currency : "JPY";
        if (preferences.Left is double left && preferences.Top is double top &&
            double.IsFinite(left) && double.IsFinite(top) &&
            left >= SystemParameters.VirtualScreenLeft && top >= SystemParameters.VirtualScreenTop &&
            left + Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
            top + Height <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        refreshTimer.Tick += async (_, _) => await LoadRatesAsync();
        ready = true;
    }

    private string Currency => CurrencyInput.SelectedItem as string ?? "JPY";

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PriceInput.Focus();
        PriceInput.SelectAll();
        await LoadRatesAsync();
        if (!closing) refreshTimer.Start();
    }

    private async void Currency_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        SavePreferences();
        await LoadRatesAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadRatesAsync();

    private async Task LoadRatesAsync()
    {
        if (closing) return;
        rateRequest?.Cancel();
        using var request = new CancellationTokenSource();
        rateRequest = request;
        string currency = Currency;
        rates = rateService.ReadCached(currency);
        RateStatus.Text = rates is null ? "Fetching latest daily rates…" : RateDescription(rates, "Saved · refreshing");
        RefreshButton.IsEnabled = false;
        Recalculate();
        try
        {
            var result = await rateService.GetAsync(currency, request.Token);
            if (closing || request.IsCancellationRequested || Currency != currency) return;
            rates = result.Rates;
            RateStatus.Text = RateDescription(rates, result.IsCached ? "Offline · saved rates" : "Frankfurter · daily rates");
            Recalculate();
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException)
        {
            if (closing || request.IsCancellationRequested || Currency != currency) return;
            RateStatus.Text = "Cannot fetch rates. Check Internet and refresh.";
            rates = null;
            Recalculate();
        }
        finally
        {
            if (ReferenceEquals(rateRequest, request))
            {
                rateRequest = null;
                RefreshButton.IsEnabled = true;
            }
        }
    }

    private static string RateDescription(ExchangeRates value, string state) =>
        $"{state}\n1 {value.Currency} = ₩{value.Krw.ToString("0.####", CultureInfo.InvariantCulture)} · {value.KrwDate:yyyy-MM-dd}" +
        (value.UsdDate != value.KrwDate ? $"\nUSD reference: {value.UsdDate:yyyy-MM-dd}" : "");

    private void Input_Changed(object sender, TextChangedEventArgs e) { if (ready) Recalculate(); }
    private void Options_Changed(object sender, RoutedEventArgs e) { if (ready) Recalculate(); }

    private void Recalculate()
    {
        ValidationText.Text = "";
        if (!ImportCalculator.TryAmount(PriceInput.Text, out decimal price) ||
            !ImportCalculator.TryAmount(ShippingInput.Text, out decimal shipping) ||
            !ImportCalculator.TryAmount(DutyInput.Text, out decimal duty) || duty > 100)
        {
            ValidationText.Text = "Use non-negative numbers; duty must be 0–100%.";
            ClearResults("Check the amounts above");
            return;
        }
        if (rates is null) { ClearResults("Exchange rates unavailable"); return; }
        var estimate = ImportCalculator.Calculate(price, shipping, rates.Krw, rates.Usd, duty,
            UsInput.IsChecked == true, ExemptionInput.IsChecked == true);
        PriceResult.Text = Won(estimate.PriceKrw);
        ShippingResult.Text = Won(estimate.ShippingKrw);
        DutyResult.Text = Won(estimate.DutyKrw);
        VatResult.Text = Won(estimate.VatKrw);
        TotalResult.Text = Won(estimate.TotalKrw);
        ExemptionText.Text = estimate.IsExempt ? $"Within USD {(UsInput.IsChecked == true ? 200 : 150)} personal-use threshold" :
            "Duty + VAT estimated on full value incl. shipping";
    }

    private static string Won(decimal value) => "₩" + decimal.Round(value, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);

    private void ClearResults(string message)
    {
        PriceResult.Text = ShippingResult.Text = DutyResult.Text = VatResult.Text = TotalResult.Text = "—";
        ExemptionText.Text = message;
    }

    private void SavePreferences()
    {
        PersistenceStatus.Text = store.Write("settings.json", new Preferences(Currency, Left, Top))
            ? "" : "Preferences could not be saved on this device.";
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        SavePreferences();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SavePreferences();
        closing = true;
        refreshTimer.Stop();
        rateRequest?.Cancel();
        client.Dispose();
    }

    private void Customs_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Win32Exception) { RateStatus.Text = "Could not open the browser."; }
        e.Handled = true;
    }
}
