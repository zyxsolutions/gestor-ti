using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorTI.Models;

/// <summary>
/// Contrato de serviço de TI (equivale à "Assinatura" do Asaas): gera cobranças recorrentes.
/// </summary>
public class Contrato
{
    public int Id { get; set; }

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Descricao { get; set; } = "";

    public TipoServico Tipo { get; set; } = TipoServico.SuporteTecnico;

    /// <summary>Valor por período (ex.: R$ 1.500 por mês, ou R$ 4.500 por trimestre).</summary>
    public decimal Valor { get; set; }

    public Periodicidade Periodicidade { get; set; } = Periodicidade.Mensal;

    /// <summary>SLA: tempo máximo de resposta em horas.</summary>
    public int SlaHorasResposta { get; set; } = 8;

    /// <summary>Observações do SLA (horário de atendimento, escopo, etc.).</summary>
    [MaxLength(500)] public string? SlaObservacoes { get; set; }

    public DateTime DataInicio { get; set; } = DateTime.Today;

    /// <summary>Fim da vigência. Nulo = prazo indeterminado.</summary>
    public DateTime? DataFim { get; set; }

    /// <summary>Dia do mês em que a cobrança vence (1–31; meses curtos usam o último dia).</summary>
    public int DiaVencimento { get; set; } = 10;

    public StatusContrato Status { get; set; } = StatusContrato.Ativo;

    public ICollection<Cobranca> Cobrancas { get; set; } = new List<Cobranca>();

    // ---------- Derivados (não persistidos) ----------

    /// <summary>Valor normalizado por mês — base do MRR.</summary>
    [NotMapped] public decimal ValorMensalizado => Valor / (int)Periodicidade;

    [NotMapped] public string SlaTexto => $"{SlaHorasResposta}h";

    [NotMapped] public string StatusTexto => Status.ToString();

    [NotMapped]
    public int? DiasParaVencer => DataFim.HasValue ? (DataFim.Value.Date - DateTime.Today).Days : null;

    [NotMapped]
    public string VigenciaTexto => DataFim.HasValue
        ? $"até {DataFim.Value:dd/MM/yyyy}"
        : "indeterminado";

    /// <summary>Este contrato deve gerar cobrança na competência informada (1º dia do mês)?</summary>
    public bool DeveGerarEm(DateTime competencia)
    {
        if (Status != StatusContrato.Ativo) return false;

        var inicio = new DateTime(DataInicio.Year, DataInicio.Month, 1);
        if (competencia < inicio) return false;
        if (DataFim.HasValue && competencia > DataFim.Value) return false;

        var meses = (competencia.Year - inicio.Year) * 12 + competencia.Month - inicio.Month;
        return meses % (int)Periodicidade == 0;
    }

    /// <summary>Data de vencimento da cobrança para a competência (respeita meses curtos).</summary>
    public DateTime VencimentoEm(DateTime competencia)
    {
        var dia = Math.Min(Math.Max(DiaVencimento, 1), DateTime.DaysInMonth(competencia.Year, competencia.Month));
        return new DateTime(competencia.Year, competencia.Month, dia);
    }
}
