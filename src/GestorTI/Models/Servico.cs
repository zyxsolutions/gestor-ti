using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorTI.Models;

public enum StatusServico { EmAndamento, Concluido, Cancelado }

/// <summary>
/// Serviço avulso (projeto/trabalho pontual): site, sistema, instalação, consultoria…
/// Diferente do contrato (recorrente), tem valor fechado e uma anotação do que será feito.
/// </summary>
public class Servico
{
    public int Id { get; set; }

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    [Required, MaxLength(200)] public string Titulo { get; set; } = "";
    [Required, MaxLength(100)] public string Categoria { get; set; } = "Outro";

    /// <summary>Anotação: o que será feito, escopo, combinados.</summary>
    [MaxLength(4000)] public string? Descricao { get; set; }

    public decimal Valor { get; set; }
    public DateTime DataInicio { get; set; } = DateTime.Today;
    public StatusServico Status { get; set; } = StatusServico.EmAndamento;

    /// <summary>Cobrança gerada para este serviço (se houver).</summary>
    public int? CobrancaId { get; set; }
    public Cobranca? Cobranca { get; set; }

    /// <summary>Orçamento de origem (se veio de um orçamento aprovado).</summary>
    public int? OrcamentoId { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string StatusTexto => Status switch
    {
        StatusServico.EmAndamento => "Em andamento",
        StatusServico.Concluido => "Concluído",
        _ => "Cancelado"
    };

    [NotMapped] public bool PodeConcluir => Status == StatusServico.EmAndamento;
    [NotMapped] public bool PodeGerarCobranca => CobrancaId is null && Status != StatusServico.Cancelado;
    [NotMapped] public string CobrancaTexto => CobrancaId is null ? "Sem cobrança" : "Cobrança gerada";
    [NotMapped] public bool TemDescricao => !string.IsNullOrWhiteSpace(Descricao);
}
