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

    private GoodsType SelectedGoodsType => ExcludedGoodsInput.IsChecked == true ? GoodsType.ListExcluded : GoodsType.ListEligible;

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
        RateStatus.Text = rates is null ? "최신 고시 환율을 불러오는 중…" : RateDescription(rates, "저장된 환율 · 갱신 중");
        RefreshButton.IsEnabled = false;
        Recalculate();
        try
        {
            var result = await rateService.GetAsync(currency, request.Token);
            if (closing || request.IsCancellationRequested || Currency != currency) return;
            rates = result.Rates;
            RateStatus.Text = RateDescription(rates, result.IsCached ? "오프라인 · 저장된 환율" : "최신 일일 환율");
            Recalculate();
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException)
        {
            if (closing || request.IsCancellationRequested || Currency != currency) return;
            RateStatus.Text = "환율을 가져올 수 없습니다. 인터넷 연결 후 새로고침하세요.";
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
        (value.UsdDate != value.KrwDate ? $"\n달러 환산 기준일: {value.UsdDate:yyyy-MM-dd}" : "");

    private void Input_Changed(object sender, TextChangedEventArgs e) { if (ready) Recalculate(); }
    private void Options_Changed(object sender, RoutedEventArgs e) { if (ready) Recalculate(); }

    private void Recalculate()
    {
        decimal threshold = ImportCalculator.ExemptionThresholdUsd(UsInput.IsChecked == true, SelectedGoodsType);
        ThresholdText.Text = $"상품 가격 미화 {threshold:0}달러 이하 면세";
        ValidationText.Text = "";
        if (!ImportCalculator.TryAmount(PriceInput.Text, out decimal price) ||
            !ImportCalculator.TryAmount(ShippingInput.Text, out decimal shipping) ||
            !ImportCalculator.TryAmount(DutyInput.Text, out decimal duty) || duty > 100)
        {
            ValidationText.Text = "금액은 0 이상, 관세율은 0~100%로 입력하세요.";
            ClearResults("입력한 금액을 확인하세요");
            return;
        }
        AdvancedSettings.Header = $"추가 설정 · 배송비 / 관세율 {duty:0.####}%";
        if (rates is null) { ClearResults("환율을 확인한 뒤 계산합니다"); return; }
        var estimate = ImportCalculator.Calculate(price, shipping, rates.Krw, rates.Usd, duty,
            UsInput.IsChecked == true, SelectedGoodsType);
        PriceResult.Text = Won(estimate.PriceKrw);
        ShippingResult.Text = Won(estimate.ShippingKrw);
        DutyResult.Text = Won(estimate.DutyKrw);
        VatResult.Text = Won(estimate.VatKrw);
        TotalResult.Text = Won(estimate.TotalKrw);
        ExemptionText.Text = estimate.IsExempt ? $"미화 {threshold:0}달러 이하 · 관세·부가세 면제" :
            "면세 기준 초과 · 배송비 포함 전체 금액에 과세";
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
            ? "" : "설정을 저장하지 못했습니다.";
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
        catch (Win32Exception) { RateStatus.Text = "브라우저를 열 수 없습니다."; }
        e.Handled = true;
    }
}
