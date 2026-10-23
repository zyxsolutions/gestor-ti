using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorTI.Models;

public enum PerfilUsuario
{
    /// <summary>Usa o sistema (clientes, contratos, cobranças, notas), sem acesso a Configurações.</summary>
    Operador,
    /// <summary>Acesso total: configurações, backup, emissor de NFS-e e gestão de usuários.</summary>
    Administrador
}

/// <summary>Usuário do sistema. A senha NUNCA é guardada: só o hash PBKDF2 com sal único.</summary>
public class Usuario
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Nome { get; set; } = "";

    /// <summary>Nome de acesso, minúsculo, sem espaços (único).</summary>
    [Required, MaxLength(50)]
    public string Login { get; set; } = "";

    [Required, MaxLength(300)]
    public string SenhaHash { get; set; } = "";

    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Operador;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoAcesso { get; set; }

    [NotMapped] public bool EhAdministrador => Perfil == PerfilUsuario.Administrador;
    [NotMapped] public string PerfilTexto => EhAdministrador ? "Administrador" : "Operador";
    [NotMapped] public string StatusTexto => Ativo ? "Ativo" : "Inativo";
    [NotMapped] public string UltimoAcessoTexto => UltimoAcesso?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "nunca";

    [NotMapped]
    public string Iniciais
    {
        get
        {
            var partes = Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0) return "?";
            return (partes.Length == 1 ? partes[0][..1] : partes[0][..1] + partes[^1][..1]).ToUpperInvariant();
        }
    }
}
