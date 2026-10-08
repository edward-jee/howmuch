using System.Windows;

namespace howmuch.Themes;

public sealed record ThemeChoice(string Id, string Label);

public static class ThemeManager
{
    public const string DefaultTheme = "gyaru";
    public static IReadOnlyList<ThemeChoice> Choices { get; } =
        [new("gyaru", "갸루 · 핑크 반짝임"), new("classic", "클래식 · 기존 디자인")];

    public static string Normalize(string? theme) => Choices.Any(choice => choice.Id == theme)
        ? theme! : DefaultTheme;

    public static void Apply(ResourceDictionary resources, string? theme)
    {
        string file = Normalize(theme) == "classic" ? "Classic" : "Gyaru";
        var palette = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/howmuch;component/Themes/{file}.xaml")
        };
        // Only this window's palette is replaced; shared control styles stay in place.
        resources.MergedDictionaries.Clear();
        resources.MergedDictionaries.Add(palette);
    }
}
