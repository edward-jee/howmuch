using System.IO;
using System.Text.Json;

namespace howmuch.Services;

public sealed class LocalStore
{
    private readonly string directory;
    public LocalStore(string? directory = null) => this.directory = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HowMuch");

    public T? Read<T>(string name)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory, name))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return default; }
    }

    public bool Write<T>(string name, T value)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value));
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return false; }
    }
}

public sealed record Preferences(string Currency = "JPY", double? Left = null, double? Top = null,
    string Theme = "gyaru");
