namespace GestorTI.Models;

/// <summary>Tipos de serviço de TI vendidos nos contratos.</summary>
public enum TipoServico
{
    SuporteTecnico,
    Monitoramento,
    Infraestrutura,
    Redes,
    Cloud,
    Seguranca,
    Consultoria,
    Outro
}

/// <summary>
/// Periodicidade da cobrança. O valor numérico é a quantidade de meses entre cobranças
/// (usado para gerar cobranças e para calcular o MRR).
/// </summary>
public enum Periodicidade
{
    Mensal = 1,
    Trimestral = 3,
    Semestral = 6,
    Anual = 12
}

public enum StatusContrato { Ativo, Suspenso, Encerrado }

/// <summary>"Vencida" não é gravada: é derivada (Pendente + vencimento no passado).</summary>
public enum StatusCobranca { Pendente, Paga, Cancelada }

public enum FormaPagamento { Pix, Boleto, Transferencia, Cartao, Dinheiro, Outro }

public enum TipoLancamento { Entrada, Saida }

public enum StatusNfse
{
    /// <summary>Gravada localmente, ainda não enviada (ex.: sem internet). Será reenviada pelo serviço de background.</summary>
    PendenteEnvio,
    /// <summary>Enviada ao provedor; aguardando autorização da prefeitura/Receita.</summary>
    Processando,
    Autorizada,
    Rejeitada,
    Cancelada
}
