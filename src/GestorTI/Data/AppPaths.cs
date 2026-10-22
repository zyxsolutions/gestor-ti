namespace GestorTI.Data;

/// <summary>
/// Caminhos padrão do app. Os DADOS ficam em %LOCALAPPDATA%\GestorTI-Dados (não exige admin).
/// Atenção: NÃO usar %LOCALAPPDATA%\GestorTI — é a pasta onde o instalador (Velopack) coloca o programa,
/// e ela é apagada na desinstalação/atualização.
/// </summary>
public static class AppPaths
{
    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string Root { get; } = Path.Combine(LocalAppData, "GestorTI-Dados");

    /// <summary>Pasta usada nas versões antigas (antes do instalador).</summary>
    private static string RootLegado => Path.Combine(LocalAppData, "GestorTI");

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
        MigrarDadosLegados();
        Directory.CreateDirectory(NotasPadrao);
    }

    /// <summary>Se existir banco na pasta antiga e nenhum na nova, move os dados para a nova.</summary>
    private static void MigrarDadosLegados()
    {
        try
        {
            var dbAntigo = Path.Combine(RootLegado, "gestor.db");
            if (!File.Exists(dbAntigo) || File.Exists(DbFile)) return;

            File.Move(dbAntigo, DbFile);
            var settingsAntigo = Path.Combine(RootLegado, "settings.json");
            if (File.Exists(settingsAntigo)) File.Move(settingsAntigo, SettingsFile);
            var notasAntigas = Path.Combine(RootLegado, "NotasFiscais");
            if (Directory.Exists(notasAntigas) && !Directory.Exists(NotasPadrao))
                Directory.Move(notasAntigas, NotasPadrao);
        }
        catch { /* melhor esforço: na pior hipótese o app começa com banco novo e os dados antigos ficam na pasta antiga */ }
    }
}

public static class Util
{
    public static string SomenteDigitos(string? s) =>
        new((s ?? "").Where(char.IsDigit).ToArray());
}
