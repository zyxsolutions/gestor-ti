namespace GestorTI.Data;

/// <summary>Caminhos padrão do app. Tudo em %LOCALAPPDATA%\GestorTI (não exige admin).</summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GestorTI");

    public static string DbFile => Path.Combine(Root, "gestor.db");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string NotasPadrao => Path.Combine(Root, "NotasFiscais");

    /// <summary>Pasta padrão de backup: dentro do OneDrive, se existir.</summary>
    public static string BackupPadrao
    {
        get
        {
            var oneDrive = Environment.GetEnvironmentVariable("OneDrive")
                           ?? Environment.GetEnvironmentVariable("OneDriveConsumer")
                           ?? Environment.GetEnvironmentVariable("OneDriveCommercial")
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive");
            return Path.Combine(oneDrive, "GestorTI-Backups");
        }
    }

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(NotasPadrao);
    }
}

public static class Util
{
    public static string SomenteDigitos(string? s) =>
        new((s ?? "").Where(char.IsDigit).ToArray());
}
