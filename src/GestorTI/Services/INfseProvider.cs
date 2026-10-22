using GestorTI.Models;

namespace GestorTI.Services;

/// <summary>
/// Abstração do provedor de NFS-e. O app só conhece esta interface; trocar Focus NFe por
/// outro provedor (ou por um fake nos testes) não afeta o resto do sistema.
/// </summary>
public interface INfseProvider
{
    /// <summary>Envia a DPS/nota. Resposta normalmente é "processando" — a autorização é assíncrona.</summary>
    Task<NfseResultado> EmitirAsync(NfseEmissao emissao, CancellationToken ct);

    /// <summary>Consulta pela referência. <see cref="NfseResultado.Encontrada"/> = false se o provedor não conhece a referência.</summary>
    Task<NfseResultado> ConsultarAsync(string referencia, CancellationToken ct);

    Task<NfseResultado> CancelarAsync(string referencia, string justificativa, CancellationToken ct);

    /// <summary>Baixa um arquivo (PDF/XML). Aceita URL absoluta ou caminho relativo do provedor.</summary>
    Task<byte[]> BaixarAsync(string urlOuCaminho, CancellationToken ct);
}

/// <summary>Dados necessários para emitir (independentes do provedor).</summary>
public sealed record NfseEmissao(
    string Referencia,
    DateTimeOffset DataEmissao,
    DateTime Competencia,
    // Prestador
    string CnpjPrestador,
    string InscricaoMunicipal,
    string CodigoMunicipioEmissor,
    int CodigoOpcaoSimplesNacional,
    // Serviço
    string CodigoTributacaoNacional,
    string? CodigoNbs,
    string Descricao,
    decimal Valor,
    // Tomador
    string DocumentoTomador,
    string NomeTomador,
    string? EmailTomador,
    string? TelefoneTomador,
    string? CepTomador,
    string? LogradouroTomador,
    string? NumeroTomador,
    string? BairroTomador,
    string? CodigoMunicipioTomador);

/// <summary>Resultado normalizado, independente do formato do provedor.</summary>
public sealed class NfseResultado
{
    public bool Encontrada { get; init; } = true;
    public StatusNfse Status { get; init; }
    public string? Numero { get; init; }
    public string? CodigoVerificacao { get; init; }
    public string? UrlPdf { get; init; }
    public string? UrlXml { get; init; }
    public string? Mensagem { get; init; }

    public static NfseResultado NaoEncontrada() => new() { Encontrada = false, Status = StatusNfse.PendenteEnvio };
}
