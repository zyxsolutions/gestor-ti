using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorTI.Models;

/// <summary>Cliente (tomador do serviço). Equivale ao "Cliente" do Asaas.</summary>
public class Cliente
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Nome { get; set; } = "";

    /// <summary>CPF (11) ou CNPJ (14), somente dígitos.</summary>
    [Required, MaxLength(14)]
    public string CpfCnpj { get; set; } = "";

    [MaxLength(200)] public string? Email { get; set; }
    [MaxLength(20)] public string? Telefone { get; set; }

    // Endereço: obrigatório para emitir NFS-e (o provedor exige o código IBGE do município do tomador).
    [MaxLength(8)] public string? Cep { get; set; }
    [MaxLength(200)] public string? Logradouro { get; set; }
    [MaxLength(20)] public string? Numero { get; set; }
    [MaxLength(100)] public string? Bairro { get; set; }
    [MaxLength(7)] public string? CodigoMunicipioIbge { get; set; }
    [MaxLength(2)] public string? Uf { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Contrato> Contratos { get; set; } = new List<Contrato>();
    public ICollection<Cobranca> Cobrancas { get; set; } = new List<Cobranca>();

    [NotMapped]
    public string DocumentoFormatado => CpfCnpj.Length switch
    {
        11 => $"{CpfCnpj[..3]}.{CpfCnpj[3..6]}.{CpfCnpj[6..9]}-{CpfCnpj[9..]}",
        14 => $"{CpfCnpj[..2]}.{CpfCnpj[2..5]}.{CpfCnpj[5..8]}/{CpfCnpj[8..12]}-{CpfCnpj[12..]}",
        _ => CpfCnpj
    };

    [NotMapped]
    public string Contato => string.Join(" · ", new[] { Email, Telefone }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
