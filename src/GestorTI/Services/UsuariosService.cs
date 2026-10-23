using GestorTI.Data;
using GestorTI.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.Services;

/// <summary>
/// Cadastro de usuários (somente administradores). Regras que evitam travar o sistema:
///  • sempre deve restar ao menos 1 administrador ativo;
///  • ninguém pode excluir, desativar ou rebaixar a si mesmo.
/// </summary>
public sealed class UsuariosService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SessaoService _sessao;

    public UsuariosService(IDbContextFactory<AppDbContext> factory, SessaoService sessao)
    {
        _factory = factory; _sessao = sessao;
    }

    private void ExigirAdministrador()
    {
        if (!_sessao.EhAdministrador)
            throw new InvalidOperationException("Somente administradores podem gerenciar usuários.");
    }

    public async Task<IReadOnlyList<Usuario>> ListarAsync()
    {
        ExigirAdministrador();
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Usuarios.AsNoTracking().OrderBy(u => u.Nome).ToListAsync();
    }

    public async Task CriarAsync(string? nome, string? login, string? senha, string? confirmar, PerfilUsuario perfil)
    {
        ExigirAdministrador();
        if (string.IsNullOrWhiteSpace(nome)) throw new InvalidOperationException("Informe o nome.");
        var chave = AuthService.ValidarLogin(login);
        AuthService.ValidarSenha(senha, confirmar);

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Usuarios.AnyAsync(u => u.Login == chave))
            throw new InvalidOperationException("Já existe um usuário com este login.");

        var hash = await Task.Run(() => SenhaHasher.Gerar(senha!));
        db.Usuarios.Add(new Usuario { Nome = nome.Trim(), Login = chave, SenhaHash = hash, Perfil = perfil });
        await db.SaveChangesAsync();
    }

    /// <param name="novaSenha">Vazia = mantém a senha atual.</param>
    public async Task AtualizarAsync(int id, string? nome, string? login, PerfilUsuario perfil, bool ativo,
        string? novaSenha, string? confirmar)
    {
        ExigirAdministrador();
        if (string.IsNullOrWhiteSpace(nome)) throw new InvalidOperationException("Informe o nome.");
        var chave = AuthService.ValidarLogin(login);
        var trocarSenha = !string.IsNullOrEmpty(novaSenha) || !string.IsNullOrEmpty(confirmar);
        if (trocarSenha) AuthService.ValidarSenha(novaSenha, confirmar);

        await using var db = await _factory.CreateDbContextAsync();
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new InvalidOperationException("Usuário não encontrado (foi excluído?).");

        if (await db.Usuarios.AnyAsync(x => x.Login == chave && x.Id != id))
            throw new InvalidOperationException("Já existe outro usuário com este login.");

        var vaiPerderAdmin = u.Perfil == PerfilUsuario.Administrador && u.Ativo &&
                             (perfil != PerfilUsuario.Administrador || !ativo);
        if (vaiPerderAdmin)
        {
            if (_sessao.Atual?.Id == id)
                throw new InvalidOperationException("Você não pode desativar nem rebaixar o seu próprio usuário.");
            await GarantirOutroAdministradorAsync(db, id);
        }

        u.Nome = nome.Trim();
        u.Login = chave;
        u.Perfil = perfil;
        u.Ativo = ativo;
        if (trocarSenha) u.SenhaHash = await Task.Run(() => SenhaHasher.Gerar(novaSenha!));

        await db.SaveChangesAsync();

        if (_sessao.Atual?.Id == id) _sessao.Iniciar(u); // mantém nome/login da sessão atualizados
    }

    public async Task ExcluirAsync(int id)
    {
        ExigirAdministrador();
        if (_sessao.Atual?.Id == id)
            throw new InvalidOperationException("Você não pode excluir o seu próprio usuário.");

        await using var db = await _factory.CreateDbContextAsync();
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new InvalidOperationException("Usuário não encontrado.");

        if (u.Perfil == PerfilUsuario.Administrador && u.Ativo)
            await GarantirOutroAdministradorAsync(db, id);

        db.Usuarios.Remove(u);
        await db.SaveChangesAsync();
    }

    private static async Task GarantirOutroAdministradorAsync(AppDbContext db, int idIgnorado)
    {
        var outro = await db.Usuarios.AnyAsync(x =>
            x.Id != idIgnorado && x.Ativo && x.Perfil == PerfilUsuario.Administrador);
        if (!outro)
            throw new InvalidOperationException("Precisa existir pelo menos um administrador ativo.");
    }
}
