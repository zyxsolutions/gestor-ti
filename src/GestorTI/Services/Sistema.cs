using System.Diagnostics;

namespace GestorTI.Services;

/// <summary>Abrir links, pastas e arquivos no aplicativo padrão — em Windows, Linux e macOS.</summary>
public static class Sistema
{
    /// <summary>Abre uma URL (navegador/WhatsApp) ou uma pasta/arquivo.</summary>
    public static void Abrir(string alvo)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo(alvo) { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open", $"\"{alvo}\"")
            { UseShellExecute = false });
    }

    /// <summary>Abre a pasta do arquivo com ele selecionado (no Linux, só abre a pasta).</summary>
    public static void MostrarArquivo(string arquivo)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{arquivo}\"") { UseShellExecute = false });
        else if (OperatingSystem.IsMacOS())
            Process.Start(new ProcessStartInfo("open", $"-R \"{arquivo}\"") { UseShellExecute = false });
        else
            Abrir(Path.GetDirectoryName(arquivo)!);
    }

    /// <summary>
    /// Telefone brasileiro → formato internacional do WhatsApp (só dígitos, com 55).
    /// Devolve null se não parecer um número válido.
    /// </summary>
    public static string? NormalizarWhatsApp(string? telefone)
    {
        var d = new string((telefone ?? "").Where(char.IsDigit).ToArray()).TrimStart('0');
        if (d.Length is 10 or 11) return "55" + d;          // DDD + número
        if (d.Length is 12 or 13 && d.StartsWith("55")) return d;
        if (d.Length >= 11) return d;                        // número internacional já com código do país
        return null;
    }

    public static string LinkWhatsApp(string numero, string mensagem) =>
        $"https://wa.me/{numero}?text={Uri.EscapeDataString(mensagem)}";
}
