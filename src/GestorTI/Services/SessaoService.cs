using GestorTI.Models;

namespace GestorTI.Services;

/// <summary>Quem está logado agora. Fica só em memória: fechar o app encerra a sessão.</summary>
public sealed class SessaoService
{
    public Usuario? Atual { get; private set; }
    public bool EstaAutenticado => Atual is not null;
    public bool EhAdministrador => Atual is { Ativo: true, Perfil: PerfilUsuario.Administrador };

    public void Iniciar(Usuario usuario) => Atual = usuario;
    public void Encerrar() => Atual = null;
}
