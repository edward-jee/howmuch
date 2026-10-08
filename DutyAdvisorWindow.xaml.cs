using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using howmuch.Core;
using howmuch.Themes;

namespace howmuch;

public partial class DutyAdvisorWindow : Window
{
    private bool ready;
    private DutyAdvice advice = DutyAdvisor.Estimate(null, null, DutyTreatment.Representative, "");
    public DutyAdvice? AppliedAdvice { get; private set; }

    public DutyAdvisorWindow(string theme)
    {
        InitializeComponent();
        ThemeManager.Apply(Resources, theme);
        CategoryInput.ItemsSource = DutyAdvisor.Categories;
        ready = true;
        UpdateAdvice();
        Loaded += (_, _) => CategoryInput.Focus();
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        ProfileInput.SelectedIndex = -1;
        ProfileInput.ItemsSource = DutyAdvisor.Profiles.Where(profile => profile.Category == CategoryInput.SelectedValue as string).ToArray();
        UpdateAdvice();
    }

    private void Answer_Changed(object sender, SelectionChangedEventArgs e) { if (ready) UpdateAdvice(); }
    private void Rate_Changed(object sender, TextChangedEventArgs e) { if (ready) UpdateAdvice(); }

    private void UpdateAdvice()
    {
        var treatment = (DutyTreatment)TreatmentInput.SelectedIndex;
        ConfirmedRatePanel.Visibility = treatment == DutyTreatment.ConfirmedRate ? Visibility.Visible : Visibility.Collapsed;
        advice = DutyAdvisor.Estimate(CategoryInput.SelectedValue as string, ProfileInput.SelectedValue as string,
            treatment, ConfirmedRateInput.Text);
        RateResult.Text = advice.Rate is decimal rate ? $"참고 관세율 {rate:0.####}%" : "품목 확인이 필요해요";
        ExplanationText.Text = advice.Explanation;
        GoodsTypeText.Text = advice.GoodsType switch
        {
            GoodsType.ListEligible => "적용 시 물품 구분: 일반 물품(목록 통관 대상)",
            GoodsType.ListExcluded => "적용 시 물품 구분: 목록통관 제외 물품",
            _ => "현재 물품 구분을 유지합니다."
        };
        SourceLink.NavigateUri = new Uri(advice.SourceUrl);
        ApplyButton.IsEnabled = advice.CanApply;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        UpdateAdvice();
        if (!advice.CanApply) return;
        AppliedAdvice = advice;
        DialogResult = true;
    }

    private void Source_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Win32Exception) { ExplanationText.Text = "브라우저를 열 수 없습니다. 관세청 홈페이지에서 품목별 세율을 확인하세요."; }
        e.Handled = true;
    }
}
