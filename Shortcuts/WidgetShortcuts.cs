using System.Windows.Input;

namespace howmuch.Shortcuts;

public enum ShortcutAction
{
    FocusPrice, Jpy, Usd, Eur, ToggleUsa, GeneralGoods, ExcludedGoods,
    Refresh, ToggleTheme, ToggleSettings, Minimize, Close, Help, DismissHelp
}

public sealed record WidgetShortcut(ShortcutAction Action, Key Key, ModifierKeys Modifiers,
    string Keys, string Description)
{
    public RoutedCommand Command { get; } = new(Action.ToString(), typeof(WidgetShortcuts));
}

public static class WidgetShortcuts
{
    public static IReadOnlyList<WidgetShortcut> All { get; } =
    [
        new(ShortcutAction.FocusPrice, Key.L, ModifierKeys.Control, "Ctrl+L", "가격 입력으로 이동·전체 선택"),
        new(ShortcutAction.Jpy, Key.D1, ModifierKeys.Control, "Ctrl+1", "통화: 일본 엔 (JPY)"),
        new(ShortcutAction.Usd, Key.D2, ModifierKeys.Control, "Ctrl+2", "통화: 미국 달러 (USD)"),
        new(ShortcutAction.Eur, Key.D3, ModifierKeys.Control, "Ctrl+3", "통화: 유로 (EUR)"),
        new(ShortcutAction.ToggleUsa, Key.U, ModifierKeys.Control, "Ctrl+U", "미국 발송 선택·해제"),
        new(ShortcutAction.GeneralGoods, Key.G, ModifierKeys.Control, "Ctrl+G", "일반 물품 선택"),
        new(ShortcutAction.ExcludedGoods, Key.G, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+G", "목록통관 제외 물품 선택"),
        new(ShortcutAction.Refresh, Key.F5, ModifierKeys.None, "F5", "환율 새로고침"),
        new(ShortcutAction.ToggleTheme, Key.T, ModifierKeys.Control, "Ctrl+T", "갸루·클래식 테마 전환"),
        new(ShortcutAction.ToggleSettings, Key.OemComma, ModifierKeys.Control, "Ctrl+,", "추가 설정 펼치기·접기"),
        new(ShortcutAction.Minimize, Key.M, ModifierKeys.Control, "Ctrl+M", "작업 표시줄로 최소화"),
        new(ShortcutAction.Close, Key.W, ModifierKeys.Control, "Ctrl+W", "위젯 종료"),
        new(ShortcutAction.Help, Key.F1, ModifierKeys.None, "F1", "단축키 안내 열기·닫기"),
        new(ShortcutAction.DismissHelp, Key.Escape, ModifierKeys.None, "Esc", "단축키 안내 닫기")
    ];
}
