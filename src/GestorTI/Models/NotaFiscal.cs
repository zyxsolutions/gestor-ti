using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorTI.Models;

/// <summary>
/// NFS-e. É gravada ANTES de qualquer chamada de rede (offline-first): se a internet cair,
/// o registro fica como <see cref="StatusNfse.PendenteEnvio"/> e é reenviado depois.
/// </summary>
public class NotaFiscal
{
    public int Id { get; set; }

    public int CobrancaId { get; set; }
    public Cobranca Cobranca { get; set; } = null!;

    /// <summary>
    /// Referência única enviada ao provedor (idempotência). Reenviar com a mesma referência
    /// nunca duplica a nota — é a base da estratégia de retry.
    /// </summary>
    [Required, MaxLength(60)]
    public string Referencia { get; set; } = "";

    public StatusNfse Status { get; set; } = StatusNfse.PendenteEnvio;

    [MaxLength(30)] public string? Numero { get; set; }
    [MaxLength(60)] public string? CodigoVerificacao { get; set; }

    public decimal ValorServicos { get; set; }
    [MaxLength(2000)] public string Discriminacao { get; set; } = "";

    /// <summary>Mensagem do provedor (erro de validação, motivo da rejeição) ou aviso local (ex.: sem internet).</summary>
    [MaxLength(2000)] public string? Mensagem { get; set; }

    // Arquivos baixados para o Windows
    [MaxLength(500)] public string? CaminhoPdf { get; set; }
    [MaxLength(500)] public string? CaminhoXml { get; set; }

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public DateTime? AutorizadaEm { get; set; }
    public DateTime? UltimaConsulta { get; set; }
    public int Tentativas { get; set; }

    [NotMapped]
    public string StatusTexto => Status switch
    {
        StatusNfse.PendenteEnvio => "Aguardando envio",
        _ => Status.ToString()
    };

    [NotMapped] public bool TemPdf => !string.IsNullOrEmpty(CaminhoPdf);
    [NotMapped] public bool TemXml => !string.IsNullOrEmpty(CaminhoXml);
    [NotMapped] public bool EmAndamento => Status is StatusNfse.PendenteEnvio or StatusNfse.Processando;
}
