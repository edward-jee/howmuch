using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using howmuch;
using howmuch.Services;
using howmuch.Themes;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HowMuch-widget-" + Guid.NewGuid());
        var store = new LocalStore(directory);
        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "settings.json"), "{\"Currency\":\"XYZ\"}");
        var handler = new DelayedRates();
        var window = new MainWindow(store, new HttpClient(handler));
        int exitCode = 0;
        window.Loaded += async (_, _) =>
        {
            try
            {
                await Task.Delay(300);
                window.UpdateLayout();
                var currency = (ComboBox)window.FindName("CurrencyInput");
                Require((string)currency.SelectedItem == "JPY", "Default JPY");
                Require(currency.Items.Cast<string>().Take(3).SequenceEqual(new[] { "JPY", "USD", "EUR" }) &&
                    currency.Items.Count == 14, "Prioritized and expanded source currencies");
                Require(((RadioButton)window.FindName("GeneralGoodsInput")).IsChecked == true, "Default general goods");
                Require(((Expander)window.FindName("AdvancedSettings")).IsExpanded == false, "Optional settings initially hidden");
                Require(window.Title == "얼마일까 · 해외직구 계산기", "Korean window title");
                Require(window.Topmost && window.ResizeMode == ResizeMode.NoResize, "Topmost and fixed size");
                var theme = (ComboBox)window.FindName("ThemeInput");
                Require((string)theme.SelectedValue == "gyaru" && window.Background is LinearGradientBrush, "Gyaru is default including legacy settings");
                Require(ThemeManager.Normalize("unknown") == "gyaru", "Unknown theme falls back to default");
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
                window.UpdateLayout();
                Require(((FrameworkElement)window.FindName("ThemeInput")).TransformToAncestor(advanced).Transform(new Point()).Y >
                    ((FrameworkElement)window.FindName("DutyInput")).TransformToAncestor(advanced).Transform(new Point()).Y,
                    "Theme selection is below shipping/duty settings");
                TestDutyDialog(window, dialog =>
                {
                    var category = (ComboBox)dialog.FindName("CategoryInput");
                    var profile = (ComboBox)dialog.FindName("ProfileInput");
                    var apply = (Button)dialog.FindName("ApplyButton");
                    Require(!apply.IsEnabled, "No advisor rate before answers");
                    category.SelectedValue = "clothes";
                    profile.SelectedValue = "cotton-shirt";
                    Require(apply.IsEnabled && ((TextBlock)dialog.FindName("RateResult")).Text.Contains("13%"), "Popup estimates selected clothing tariff");
                    dialog.UpdateLayout();
                    CapturePreview(dialog, "duty-advisor");
                    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                Require(((TextBox)window.FindName("DutyInput")).Text == "13" &&
                    ((RadioButton)window.FindName("GeneralGoodsInput")).IsChecked == true, "Apply transfers rate and classification");
                Require(((TextBlock)window.FindName("DutyResult")).Text == "₩0", "Applied general goods uses US $200 exemption");
                TestDutyDialog(window, dialog =>
                {
                    ((ComboBox)dialog.FindName("CategoryInput")).SelectedValue = "food";
                    ((ComboBox)dialog.FindName("ProfileInput")).SelectedValue = "supplement";
                    Require(!((Button)dialog.FindName("ApplyButton")).IsEnabled, "Supplement tariff is not guessed");
                    ((ComboBox)dialog.FindName("TreatmentInput")).SelectedIndex = 1;
                    ((TextBox)dialog.FindName("ConfirmedRateInput")).Text = "8";
                    ((Button)dialog.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                Require(((TextBox)window.FindName("DutyInput")).Text == "8" &&
                    ((RadioButton)window.FindName("ExcludedGoodsInput")).IsChecked == true, "Food advice transfers confirmed rate and excluded classification");
                Require(((TextBlock)window.FindName("ThresholdText")).Text.Contains("150"), "Applied food classification uses $150 threshold");
                TestDutyDialog(window, dialog =>
                {
                    ((ComboBox)dialog.FindName("CategoryInput")).SelectedValue = "clothes";
                    ((ComboBox)dialog.FindName("ProfileInput")).SelectedValue = "cotton-shirt";
                    ((ComboBox)dialog.FindName("CategoryInput")).SelectedValue = "shoes";
                    Require(!((Button)dialog.FindName("ApplyButton")).IsEnabled, "Category changes reset the old profile");
                    ((ComboBox)dialog.FindName("ProfileInput")).SelectedValue = "sports-shoe";
                    ((ComboBox)dialog.FindName("TreatmentInput")).SelectedIndex = 2;
                    Require(!((Button)dialog.FindName("ApplyButton")).IsEnabled, "Special taxes block application");
                    dialog.DialogResult = false;
                });
                Require(((TextBox)window.FindName("DutyInput")).Text == "8" &&
                    ((RadioButton)window.FindName("ExcludedGoodsInput")).IsChecked == true, "Cancel keeps existing inputs");
                ((TextBox)window.FindName("DutyInput")).Text = "13";
                advanced.IsExpanded = false;
                theme.SelectedValue = "classic";
                window.UpdateLayout();
                CapturePreview(window, "classic");
                Require(window.Background is SolidColorBrush classic && classic.Color == Color.FromRgb(245, 247, 251), "Classic preserves original palette");
                Require((Visibility)window.FindResource("DecorationVisibility") == Visibility.Collapsed, "Classic hides gyaru decorations");
                Require(store.Read<Preferences>("settings.json")?.Theme == "classic", "Selected theme saved immediately");
                var restored = new MainWindow(store, new HttpClient(new DelayedRates()));
                Require((string)((ComboBox)restored.FindName("ThemeInput")).SelectedValue == "classic", "Theme restored on next launch");
                restored.Close();
                Require(((TextBlock)window.FindName("TotalResult")).Text == "₩321,937", "Theme change preserves calculated amounts");
                theme.SelectedValue = "gyaru";
                window.UpdateLayout();
                CapturePreview(window, "gyaru");
                Require(window.Background is LinearGradientBrush && store.Read<Preferences>("settings.json")?.Theme == "gyaru", "Switch back to gyaru and persist");
                advanced.IsExpanded = false;
                window.Activate();
                price.Focus();
                RunShortcut(window, Key.L, ModifierKeys.Control);
                Require(price.IsKeyboardFocused && price.SelectedText == price.Text, "Shortcut focuses and selects price");
                RunShortcut(window, Key.U, ModifierKeys.Control);
                Require(((CheckBox)window.FindName("UsInput")).IsChecked == false, "Shortcut toggles US shipment");
                RunShortcut(window, Key.U, ModifierKeys.Control);
                RunShortcut(window, Key.G, ModifierKeys.Control);
                Require(((RadioButton)window.FindName("GeneralGoodsInput")).IsChecked == true &&
                    ((TextBlock)window.FindName("ThresholdText")).Text.Contains("200"), "Shortcut selects general goods and recalculates");
                RunShortcut(window, Key.G, ModifierKeys.Control | ModifierKeys.Shift);
                Require(((RadioButton)window.FindName("ExcludedGoodsInput")).IsChecked == true &&
                    ((TextBlock)window.FindName("ThresholdText")).Text.Contains("150"), "Shortcut selects excluded goods and recalculates");
                RunShortcut(window, Key.OemComma, ModifierKeys.Control);
                Require(advanced.IsExpanded, "Shortcut opens settings");
                RunShortcut(window, Key.OemComma, ModifierKeys.Control);
                RunShortcut(window, Key.T, ModifierKeys.Control);
                Require((string)theme.SelectedValue == "classic", "Shortcut switches theme");
                RunShortcut(window, Key.T, ModifierKeys.Control);
                RunShortcut(window, Key.D1, ModifierKeys.Control);
                await Task.Delay(100);
                Require((string)currency.SelectedItem == "JPY", "Shortcut selects JPY");
                RunShortcut(window, Key.D3, ModifierKeys.Control);
                await Task.Delay(300);
                Require((string)currency.SelectedItem == "EUR", "Shortcut selects EUR");
                RunShortcut(window, Key.D2, ModifierKeys.Control);
                await Task.Delay(100);
                Require((string)currency.SelectedItem == "USD" && store.Read<Preferences>("settings.json")?.Currency == "USD", "Shortcut selects and saves USD");
                currency.SelectedItem = "CNY";
                await Task.Delay(100);
                Require(((TextBlock)window.FindName("PriceResult")).Text == "₩33,250" &&
                    store.Read<Preferences>("settings.json")?.Currency == "CNY", "Additional currency conversion and persistence");
                currency.SelectedItem = "VND";
                await Task.Delay(100);
                Require(((TextBlock)window.FindName("PriceResult")).Text == "₩9" &&
                    ((TextBlock)window.FindName("VatResult")).Text == "₩0", "Small decimal VND rates and USD-based exemption");
                RunShortcut(window, Key.D2, ModifierKeys.Control);
                await Task.Delay(100);
                int requests = handler.RequestCount;
                RunShortcut(window, Key.F5, ModifierKeys.None);
                Require(!FindShortcut(window, Key.F5, ModifierKeys.None).CanExecute(null, window), "Refresh shortcut disabled while loading");
                await Task.Delay(100);
                Require(handler.RequestCount == requests + 1, "Refresh shortcut requests fresh rates");
                RunShortcut(window, Key.F1, ModifierKeys.None);
                Require(((Grid)window.FindName("HelpOverlay")).Visibility == Visibility.Visible &&
                    ((Button)window.FindName("HelpCloseButton")).IsKeyboardFocused, "Help shortcut opens accessible Korean guide");
                Require(!FindShortcut(window, Key.T, ModifierKeys.Control).CanExecute(null, window), "Help prevents shortcuts changing covered inputs");
                window.UpdateLayout();
                CapturePreview(window, "shortcuts");
                RunShortcut(window, Key.Escape, ModifierKeys.None);
                Require(((Grid)window.FindName("HelpOverlay")).Visibility == Visibility.Collapsed && price.IsKeyboardFocused, "Escape closes guide and restores focus");
                RunShortcut(window, Key.M, ModifierKeys.Control);
                Require(window.WindowState == WindowState.Minimized, "Shortcut minimizes widget");
                window.WindowState = WindowState.Normal;
                window.UpdateLayout();
                foreach (var element in Descendants(window).OfType<FrameworkElement>()
                    .Where(e => e.IsVisible && e.ActualHeight > 0 && e is TextBlock or TextBox or ComboBox or Button or CheckBox or RadioButton))
                {
                    var location = element.TransformToAncestor(window).Transform(new Point());
                    Require(location.Y + element.ActualHeight <= window.ActualHeight + 1, $"Layout fits: {element.GetType().Name} {element.Name} ends at {location.Y + element.ActualHeight:0.0}");
                    Require(location.X >= -1 && location.X + element.ActualWidth <= window.ActualWidth + 1, $"Horizontal layout fits: {element.GetType().Name} {element.Name}");
                }
                RunShortcut(window, Key.W, ModifierKeys.Control);
                Require(!window.IsVisible, "Shortcut closes widget");
                Console.WriteLine("Passed native Korean WPF layout, duty-advisor dialogs, customs options, themes, shortcuts, persistence, and request-race checks.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); exitCode = 1; }
            finally { if (window.IsVisible) window.Close(); }
        };
        new Application().Run(window);
        if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
        return exitCode;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static RoutedCommand FindShortcut(Window window, Key key, ModifierKeys modifiers) =>
        (RoutedCommand)window.InputBindings.OfType<KeyBinding>()
            .Single(binding => binding.Key == key && binding.Modifiers == modifiers).Command;

    private static void TestDutyDialog(Window owner, Action<DutyAdvisorWindow> check)
    {
        Exception? failure = null;
        owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = owner.OwnedWindows.OfType<DutyAdvisorWindow>().Single();
            try { check(dialog); }
            catch (Exception ex) { failure = ex; dialog.DialogResult = false; }
        }));
        ((Button)owner.FindName("DutyAssistButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (failure is not null) throw failure;
    }

    private static void RunShortcut(Window window, Key key, ModifierKeys modifiers)
    {
        var command = FindShortcut(window, key, modifiers);
        IInputElement target = Keyboard.FocusedElement ?? window;
        Require(command.CanExecute(null, target), $"Shortcut available: {modifiers}+{key}");
        command.Execute(null, target);
    }

    private static void CapturePreview(Window window, string name)
    {
        if (!Environment.GetCommandLineArgs().Contains("--capture-themes")) return;
        string directory = System.IO.Path.Combine(Environment.CurrentDirectory, "artifacts", "theme-previews");
        System.IO.Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = System.IO.File.Create(System.IO.Path.Combine(directory, name + ".png"));
        encoder.Save(output);
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
        public int RequestCount { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            RequestCount++;
            string currency = request.RequestUri!.Query.Split("base=")[1].Split('&')[0];
            // EUR deliberately completes after USD to exercise cancellation and currency guarding.
            await Task.Delay(currency == "EUR" ? 200 : 30, token);
            string date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string krw = currency switch { "JPY" => "9.5", "CNY" => "190", "VND" => "0.05", _ => "1400" };
            string usd = currency switch { "CNY" => "0.14", "VND" => "0.00004", _ => "0.0067" };
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""
                    [{"date":"{{date}}","base":"{{currency}}","quote":"KRW","rate":{{krw}}},
                     {"date":"{{date}}","base":"{{currency}}","quote":"USD","rate":{{usd}}}]
                    """)
            };
        }
    }
}
