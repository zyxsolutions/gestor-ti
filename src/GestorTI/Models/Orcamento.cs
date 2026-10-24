using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestorTI.Models;

public enum StatusOrcamento { Rascunho, Enviado, Aprovado, Recusado }

public class Orcamento
{
    public int Id { get; set; }

    /// <summary>Sequencial (1, 2, 3…), exibido como ORC-0001.</summary>
    public int Numero { get; set; }

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    [Required, MaxLength(200)] public string Titulo { get; set; } = "";

    /// <summary>Condições, prazo, forma de pagamento etc. (aparece no PDF).</summary>
    [MaxLength(4000)] public string? Observacoes { get; set; }

    public DateTime Validade { get; set; } = DateTime.Today.AddDays(15);
    public StatusOrcamento Status { get; set; } = StatusOrcamento.Rascunho;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>Serviço criado quando o orçamento foi aprovado.</summary>
    public int? ServicoId { get; set; }

    public List<OrcamentoItem> Itens { get; set; } = new();

    [NotMapped] public string NumeroTexto => $"ORC-{Numero:0000}";
    [NotMapped] public decimal Total => Itens.Sum(i => i.Total);
    [NotMapped] public bool Expirado => Status is StatusOrcamento.Rascunho or StatusOrcamento.Enviado && Validade.Date < DateTime.Today;
    [NotMapped] public string StatusTexto => Expirado ? "Expirado" : Status.ToString();
    [NotMapped] public bool PodeAprovar => Status is StatusOrcamento.Rascunho or StatusOrcamento.Enviado;
    [NotMapped] public bool PodeEditar => Status != StatusOrcamento.Aprovado;
}

public class OrcamentoItem
{
    public int Id { get; set; }
    public int OrcamentoId { get; set; }

    [Required, MaxLength(300)] public string Descricao { get; set; } = "";
    public decimal Quantidade { get; set; } = 1;
    public decimal ValorUnitario { get; set; }

    [NotMapped] public decimal Total => Quantidade * ValorUnitario;
}
