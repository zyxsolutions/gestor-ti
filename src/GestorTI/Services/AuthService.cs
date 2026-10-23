using GestorTI.Data;
using GestorTI.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.Services;

/// <summary>
/// Login, primeiro acesso e recuperação.
///
///  • Nenhuma senha padrão existe: na 1ª execução (sem usuários) o app pede para criar o administrador.
///  • Erro de login é sempre genérico ("usuário ou senha inválidos") e leva o mesmo tempo para usuário
///    inexistente e senha errada (hash falso), para não revelar quais usuários existem.
///  • 5 erros seguidos no mesmo usuário bloqueiam novas tentativas por 60 s.
///  • Esqueceu a senha do único administrador? Crie o arquivo <c>redefinir-admin.txt</c> na pasta de dados
///    (%LOCALAPPDATA%\GestorTI-Dados) e reabra o app: ele entra em modo de recuperação, que permite criar
///    um administrador ou redefinir a senha de um existente. Exige acesso ao computador, como o próprio banco.
/// </summary>
public sealed class AuthService
{
    private const int MaxFalhas = 5;
    private static readonly TimeSpan Bloqueio = TimeSpan.FromSeconds(60);
    private static readonly Lazy<string> HashFalso = new(() => SenhaHasher.Gerar("hash-falso-para-igualar-o-tempo"));

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SettingsService _settings;
    private readonly SessaoService _sessao;

    private readonly object _lock = new();
    private readonly Dictionary<string, (int Falhas, DateTime Ate)> _falhas = new();

    public AuthService(IDbContextFactory<AppDbContext> factory, SettingsService settings, SessaoService sessao)
    {
        _factory = factory; _settings = settings; _sessao = sessao;
    }

    private static string ArquivoRecuperacao => Path.Combine(AppPaths.Root, "redefinir-admin.txt");
    public bool ModoRecuperacao => File.Exists(ArquivoRecuperacao);

    /// <summary>Primeiro acesso (nenhum usuário) ou modo de recuperação: a tela pede o cadastro do administrador.</summary>
    public async Task<bool> PrecisaConfigurarAdminAsync()
    {
        if (ModoRecuperacao) return true;
        await using var db = await _factory.CreateDbContextAsync();
        return !await db.Usuarios.AnyAsync();
    }

    // ------------------------------------------------------------------ validações compartilhadas

    public static string NormalizarLogin(string? login) => (login ?? "").Trim().ToLowerInvariant();

    public static string ValidarLogin(string? login)
    {
        var l = NormalizarLogin(login);
        if (l.Length < 3 || l.Length > 50)
            throw new InvalidOperationException("O usuário deve ter entre 3 e 50 caracteres.");
        if (!l.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-'))
            throw new InvalidOperationException("Use só letras, números, ponto, hífen ou sublinhado no usuário (sem espaços).");
        return l;
    }

    public static void ValidarSenha(string? senha, string? confirmar)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length < 8)
            throw new InvalidOperationException("A senha deve ter pelo menos 8 caracteres.");
        if (senha.Length > 128)
            throw new InvalidOperationException("A senha pode ter no máximo 128 caracteres.");
        if (senha != confirmar)
            throw new InvalidOperationException("A confirmação da senha não confere.");
    }

    // ------------------------------------------------------------------ entrar

    public async Task<Usuario> EntrarAsync(string? login, string? senha)
    {
        var chave = NormalizarLogin(login);
        if (chave.Length == 0 || string.IsNullOrEmpty(senha))
            throw new InvalidOperationException("Informe usuário e senha.");

        VerificarBloqueio(chave);

        await using var db = await _factory.CreateDbContextAsync();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Login == chave);

        var hash = usuario?.SenhaHash ?? HashFalso.Value; // mesmo custo, exista ou não
        var confere = await Task.Run(() => SenhaHasher.Verificar(hash, senha));

        if (usuario is null || !confere)
        {
            RegistrarFalha(chave);
            throw new InvalidOperationException("Usuário ou senha inválidos.");
        }
        if (!usuario.Ativo)
            throw new InvalidOperationException("Este usuário está inativo. Fale com um administrador.");

        LimparFalhas(chave);
        usuario.UltimoAcesso = DateTime.UtcNow;
        await db.SaveChangesAsync();

        _sessao.Iniciar(usuario);
        _settings.Update(s => s.UltimoUsuario = usuario.Login);
        return usuario;
    }

    // ------------------------------------------------------------------ primeiro acesso / recuperação

    public async Task<Usuario> ConfigurarAdministradorAsync(string? nome, string? login, string? senha, string? confirmar)
    {
        var recuperacao = ModoRecuperacao;

        await using var db = await _factory.CreateDbContextAsync();
        if (!recuperacao && await db.Usuarios.AnyAsync())
            throw new InvalidOperationException("O administrador já foi configurado. Entre com seu usuário e senha.");

        if (string.IsNullOrWhiteSpace(nome)) throw new InvalidOperationException("Informe seu nome.");
        var chave = ValidarLogin(login);
        ValidarSenha(senha, confirmar);

        var hash = await Task.Run(() => SenhaHasher.Gerar(senha!));
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Login == chave);

        if (usuario is null)
        {
            usuario = new Usuario { Nome = nome.Trim(), Login = chave, SenhaHash = hash, Perfil = PerfilUsuario.Administrador };
            db.Usuarios.Add(usuario);
        }
        else // só chega aqui em modo de recuperação (sem recuperação a tabela estaria vazia)
        {
            usuario.SenhaHash = hash;
            usuario.Perfil = PerfilUsuario.Administrador;
            usuario.Ativo = true;
        }

        usuario.UltimoAcesso = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (recuperacao) { try { File.Delete(ArquivoRecuperacao); } catch { /* sem permissão: remova manualmente */ } }
        LimparFalhas(chave);

        _sessao.Iniciar(usuario);
        _settings.Update(s => s.UltimoUsuario = usuario.Login);
        return usuario;
    }

    // ------------------------------------------------------------------ trocar a própria senha

    public async Task AlterarSenhaAsync(int usuarioId, string? senhaAtual, string? novaSenha, string? confirmar)
    {
        if (string.IsNullOrEmpty(senhaAtual)) throw new InvalidOperationException("Informe a senha atual.");
        ValidarSenha(novaSenha, confirmar);

        await using var db = await _factory.CreateDbContextAsync();
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId)
                ?? throw new InvalidOperationException("Usuário não encontrado.");

        if (!await Task.Run(() => SenhaHasher.Verificar(u.SenhaHash, senhaAtual)))
            throw new InvalidOperationException("A senha atual está incorreta.");

        u.SenhaHash = await Task.Run(() => SenhaHasher.Gerar(novaSenha!));
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ bloqueio por tentativas

    private void VerificarBloqueio(string chave)
    {
        lock (_lock)
        {
            if (!_falhas.TryGetValue(chave, out var f) || f.Falhas < MaxFalhas) return;
            var restante = f.Ate - DateTime.UtcNow;
            if (restante > TimeSpan.Zero)
                throw new InvalidOperationException($"Muitas tentativas. Aguarde {Math.Ceiling(restante.TotalSeconds)} segundos.");
            _falhas.Remove(chave); // bloqueio expirou
        }
    }

    private void RegistrarFalha(string chave)
    {
        lock (_lock)
        {
            _falhas.TryGetValue(chave, out var f);
            var falhas = f.Falhas + 1;
            _falhas[chave] = (falhas, falhas >= MaxFalhas ? DateTime.UtcNow + Bloqueio : DateTime.UtcNow);
        }
    }

    private void LimparFalhas(string chave)
    {
        lock (_lock) _falhas.Remove(chave);
    }
}
