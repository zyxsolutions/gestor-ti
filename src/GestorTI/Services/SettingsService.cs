using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GestorTI.Data;

namespace GestorTI.Services;

public sealed class AppSettings
{
    /// <summary>"homologacao" (testes, sem validade fiscal) ou "producao".</summary>
    public string Ambiente { get; set; } = "homologacao";

    /// <summary>Token da API (gravado protegido — veja <see cref="SettingsService.FocusToken"/>).</summary>
    public string FocusTokenProtegido { get; set; } = "";

    // Dados do prestador (você, MEI)
    public string CnpjPrestador { get; set; } = "";
    public string InscricaoMunicipal { get; set; } = "";
    /// <summary>Código IBGE (7 dígitos) do município emissor.</summary>
    public string CodigoMunicipioEmissor { get; set; } = "";
    /// <summary>Código de tributação nacional (LC 116). 010701 = suporte técnico em informática.</summary>
    public string CodigoTributacaoNacional { get; set; } = "010701";
    /// <summary>Código NBS (opcional, depende do município/serviço).</summary>
    public string CodigoNbs { get; set; } = "";
    /// <summary>1 = não optante, 2 = MEI, 3 = ME/EPP (tabela do padrão nacional).</summary>
    public int CodigoOpcaoSimplesNacional { get; set; } = 2;

    // Pastas
    public string PastaNotas { get; set; } = AppPaths.NotasPadrao;
    public string PastaBackup { get; set; } = AppPaths.BackupPadrao;

    // Backup
    public int BackupIntervaloHoras { get; set; } = 24;
    public int BackupRetencao { get; set; } = 14;
    public DateTime? UltimoBackupUtc { get; set; }

    /// <summary>"Sistema" (segue o Windows), "Claro" ou "Escuro".</summary>
    public string Tema { get; set; } = "Sistema";

    /// <summary>Último login usado — só preenche o campo "Usuário" na tela de entrada (nunca a senha).</summary>
    public string UltimoUsuario { get; set; } = "";
}

/// <summary>Lê/grava settings.json. O token da API é protegido com DPAPI (usuário atual do Windows).</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _lock = new();

    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        Current = Carregar();
    }

    public string FocusToken
    {
        get => Unprotect(Current.FocusTokenProtegido);
        set => Update(s => s.FocusTokenProtegido = Protect(value));
    }

    public void Update(Action<AppSettings> mutate)
    {
        lock (_lock)
        {
            mutate(Current);
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(Current, Json));
        }
    }

    private static AppSettings Carregar()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new();
        }
        catch { /* arquivo corrompido: volta ao padrão */ }
        return new AppSettings();
    }

    private static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        var bytes = Encoding.UTF8.GetBytes(plain);
        if (OperatingSystem.IsWindows())
            return "dpapi:" + Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        return "aes:" + ProtegerComChave(bytes); // Linux/macOS: AES-GCM com chave só do usuário
    }

    // ---- Linux/macOS: chave aleatória em arquivo com permissão 0600 (só o dono lê) ----
    private static string ArquivoChave => Path.Combine(AppPaths.Root, ".chave");

    private static byte[] ObterChave()
    {
        if (File.Exists(ArquivoChave)) return File.ReadAllBytes(ArquivoChave);
        Directory.CreateDirectory(AppPaths.Root);
        var chave = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(ArquivoChave, chave);
        try { File.SetUnixFileMode(ArquivoChave, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
        return chave;
    }

    private static string ProtegerComChave(byte[] dados)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cifrado = new byte[dados.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(ObterChave(), 16);
        aes.Encrypt(nonce, dados, cifrado, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cifrado).ToArray());
    }

    private static byte[] AbrirComChave(string b64)
    {
        var t = Convert.FromBase64String(b64);
        var nonce = t[..12]; var tag = t[12..28]; var cifrado = t[28..];
        var claro = new byte[cifrado.Length];
        using var aes = new AesGcm(ObterChave(), 16);
        aes.Decrypt(nonce, cifrado, tag, claro);
        return claro;
    }

    private static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        try
        {
            if (stored.StartsWith("dpapi:", StringComparison.Ordinal) && OperatingSystem.IsWindows())
            {
                var raw = Convert.FromBase64String(stored["dpapi:".Length..]);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser));
            }
            if (stored.StartsWith("aes:", StringComparison.Ordinal))
                return Encoding.UTF8.GetString(AbrirComChave(stored["aes:".Length..]));
            if (stored.StartsWith("plain:", StringComparison.Ordinal))
                return Encoding.UTF8.GetString(Convert.FromBase64String(stored["plain:".Length..]));
        }
        catch { /* token ilegível (outro usuário/PC): o usuário precisa digitar de novo */ }
        return "";
    }
}
