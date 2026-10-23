using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorTI.Models;

/// <summary>Cobrança (recorrente, gerada por contrato, ou avulsa). Equivale à "Cobrança" do Asaas.</summary>
public class Cobranca
{
    public int Id { get; set; }

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    /// <summary>Nulo = cobrança avulsa.</summary>
    public int? ContratoId { get; set; }
    public Contrato? Contrato { get; set; }

    /// <summary>1º dia do mês de competência. Com ContratoId, forma chave única (evita cobrança duplicada).</summary>
    public DateTime? Competencia { get; set; }

    [Required, MaxLength(300)]
    public string Descricao { get; set; } = "";

    public decimal Valor { get; set; }
    public DateTime Vencimento { get; set; } = DateTime.Today;
    public DateTime? DataPagamento { get; set; }

    public StatusCobranca Status { get; set; } = StatusCobranca.Pendente;
    public FormaPagamento Forma { get; set; } = FormaPagamento.Pix;

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;

    /// <summary>Histórico de notas (uma rejeitada pode ser reemitida).</summary>
    public ICollection<NotaFiscal> Notas { get; set; } = new List<NotaFiscal>();

    // ---------- Derivados ----------

    [NotMapped]
    public bool EstaVencida => Status == StatusCobranca.Pendente && Vencimento.Date < DateTime.Today;

    /// <summary>Texto exibido na UI: inclui o estado derivado "Vencida".</summary>
    [NotMapped]
    public string StatusTexto => EstaVencida ? "Vencida" : Status.ToString();

    [NotMapped] public bool PodeReceber => Status == StatusCobranca.Pendente;

    [NotMapped] public bool PodeEditar => Status != StatusCobranca.Cancelada;
}

/// <summary>Lançamento do fluxo de caixa (entrada ou saída). Equivale ao "Extrato" do Asaas.</summary>
public class LancamentoCaixa
{
    public int Id { get; set; }
    public TipoLancamento Tipo { get; set; }

    [Required, MaxLength(300)]
    public string Descricao { get; set; } = "";

    public decimal Valor { get; set; }
    public DateTime Data { get; set; } = DateTime.Today;

    [MaxLength(100)] public string? Categoria { get; set; }

    /// <summary>Preenchido quando a entrada veio do recebimento de uma cobrança.</summary>
    public int? CobrancaId { get; set; }
    public Cobranca? Cobranca { get; set; }

    [NotMapped] public string TipoTexto => Tipo == TipoLancamento.Entrada ? "Entrada" : "Saída";

    /// <summary>Lançamentos criados por um recebimento só mudam pela cobrança de origem.</summary>
    [NotMapped] public bool PodeEditar => CobrancaId is null;

    [NotMapped] public decimal ValorComSinal => Tipo == TipoLancamento.Entrada ? Valor : -Valor;
}
