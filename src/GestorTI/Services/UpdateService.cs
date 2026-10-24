using Velopack;
using Velopack.Sources;

namespace GestorTI.Services;

/// <summary>
/// Atualização automática via GitHub Releases (Velopack).
/// Só funciona no app INSTALADO (Setup.exe no Windows, AppImage no Linux); rodando via "dotnet run" não há atualização.
/// Repositório público: não precisa de token. Se um dia ficar privado, passe um token no GithubSource.
/// </summary>
public sealed class UpdateService
{
    private const string RepoUrl = "https://github.com/zyxsolutions/gestor-ti";

    private readonly UpdateManager _mgr = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
    private UpdateInfo? _pendente;

    public string Versao => _mgr.IsInstalled ? (_mgr.CurrentVersion?.ToString() ?? "?") : "dev";

    /// <summary>Consulta o GitHub. Devolve null se não houver versão nova (ou se não estiver instalado).</summary>
    public async Task<UpdateInfo?> VerificarAsync()
    {
        if (!_mgr.IsInstalled) return null;
        _pendente = await _mgr.CheckForUpdatesAsync();
        return _pendente;
    }

    /// <summary>Baixa só o que mudou (delta), aplica e reinicia o app.</summary>
    public async Task InstalarAsync(Action<int>? progresso = null)
    {
        if (_pendente is null) throw new InvalidOperationException("Nenhuma atualização disponível.");
        await _mgr.DownloadUpdatesAsync(_pendente, progresso);
        _mgr.ApplyUpdatesAndRestart(_pendente);
    }
}
