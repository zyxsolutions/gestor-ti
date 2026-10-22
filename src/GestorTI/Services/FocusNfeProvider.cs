using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GestorTI.Models;

namespace GestorTI.Services;

/// <summary>
/// Provedor Focus NFe — NFS-e Padrão Nacional (endpoint <c>/v2/nfsen</c>).
///
/// IMPORTANTE: os nomes de campos do payload seguem a documentação pública da Focus (exemplos do
/// guia "NFSe Nacional"). Valide em HOMOLOGAÇÃO antes de produção — cada município/serviço pode
/// exigir campos adicionais (ex.: NBS). Todo o mapeamento está isolado em <see cref="MontarPayload"/>.
///
/// Convenções de erro (importantes para o modelo offline-first):
///  • falha de rede / HTTP 5xx  → lança <see cref="HttpRequestException"/> (o chamador tenta de novo depois);
///  • HTTP 401/403              → lança <see cref="InvalidOperationException"/> (token inválido: não adianta repetir);
///  • HTTP 404 na consulta      → <see cref="NfseResultado.NaoEncontrada"/>;
///  • demais 4xx (422 etc.)     → <see cref="StatusNfse.Rejeitada"/> com a mensagem do provedor.
/// </summary>
public sealed class FocusNfeProvider : INfseProvider
{
    private const string HostProducao = "https://api.focusnfe.com.br";
    private const string HostHomologacao = "https://homologacao.focusnfe.com.br";

    private readonly HttpClient _http;
    private readonly SettingsService _settings;

    public FocusNfeProvider(HttpClient http, SettingsService settings)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _settings = settings;
    }

    private string Host => _settings.Current.Ambiente == "producao" ? HostProducao : HostHomologacao;

    private HttpRequestMessage NovaRequisicao(HttpMethod metodo, string url)
    {
        var token = _settings.FocusToken;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Configure o token da API de NFS-e em Configurações.");

        var req = new HttpRequestMessage(metodo, url);
        // Focus usa HTTP Basic: token como usuário, senha vazia.
        req.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(token + ":")));
        return req;
    }

    public async Task<NfseResultado> EmitirAsync(NfseEmissao e, CancellationToken ct)
    {
        using var req = NovaRequisicao(HttpMethod.Post, $"{Host}/v2/nfsen?ref={Uri.EscapeDataString(e.Referencia)}");
        req.Content = new StringContent(JsonSerializer.Serialize(MontarPayload(e)), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        return Interpretar(resp.StatusCode, await resp.Content.ReadAsStringAsync(ct), consulta: false);
    }

    public async Task<NfseResultado> ConsultarAsync(string referencia, CancellationToken ct)
    {
        using var req = NovaRequisicao(HttpMethod.Get, $"{Host}/v2/nfsen/{Uri.EscapeDataString(referencia)}");
        using var resp = await _http.SendAsync(req, ct);
        return Interpretar(resp.StatusCode, await resp.Content.ReadAsStringAsync(ct), consulta: true);
    }

    public async Task<NfseResultado> CancelarAsync(string referencia, string justificativa, CancellationToken ct)
    {
        // Confirme o contrato exato de cancelamento do /v2/nfsen na documentação da Focus.
        using var req = NovaRequisicao(HttpMethod.Delete, $"{Host}/v2/nfsen/{Uri.EscapeDataString(referencia)}");
        req.Content = new StringContent(JsonSerializer.Serialize(new { justificativa }), Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req, ct);
        return Interpretar(resp.StatusCode, await resp.Content.ReadAsStringAsync(ct), consulta: true);
    }

    public async Task<byte[]> BaixarAsync(string urlOuCaminho, CancellationToken ct)
    {
        var url = urlOuCaminho.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? urlOuCaminho
            : Host + (urlOuCaminho.StartsWith('/') ? "" : "/") + urlOuCaminho;

        using var req = NovaRequisicao(HttpMethod.Get, url);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    // ------------------------------------------------------------------ mapeamento

    private static Dictionary<string, object?> MontarPayload(NfseEmissao e)
    {
        var p = new Dictionary<string, object?>
        {
            ["data_emissao"] = e.DataEmissao.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            ["data_competencia"] = e.Competencia.ToString("yyyy-MM-dd"),
            ["codigo_municipio_emissora"] = int.TryParse(e.CodigoMunicipioEmissor, out var mun) ? mun : 0,
            ["cnpj_prestador"] = e.CnpjPrestador,
            ["inscricao_municipal_prestador"] = e.InscricaoMunicipal,
            ["codigo_opcao_simples_nacional"] = e.CodigoOpcaoSimplesNacional,
            ["regime_especial_tributacao"] = 0,

            ["razao_social_tomador"] = e.NomeTomador,
            ["email_tomador"] = e.EmailTomador,
            ["telefone_tomador"] = e.TelefoneTomador,
            ["cep_tomador"] = e.CepTomador,
            ["logradouro_tomador"] = e.LogradouroTomador,
            ["numero_tomador"] = e.NumeroTomador,
            ["bairro_tomador"] = e.BairroTomador,
            ["codigo_municipio_tomador"] = int.TryParse(e.CodigoMunicipioTomador, out var munT) ? munT : (int?)null,

            ["codigo_municipio_prestacao"] = int.TryParse(e.CodigoMunicipioEmissor, out var munP) ? munP : 0,
            ["codigo_tributacao_nacional_iss"] = e.CodigoTributacaoNacional,
            ["codigo_nbs"] = string.IsNullOrWhiteSpace(e.CodigoNbs) ? null : e.CodigoNbs,
            ["descricao_servico"] = e.Descricao,
            ["valor_servico"] = e.Valor,
            ["tributacao_iss"] = 1,
            ["tipo_retencao_iss"] = 1
        };

        // CPF x CNPJ do tomador
        p[e.DocumentoTomador.Length == 11 ? "cpf_tomador" : "cnpj_tomador"] = e.DocumentoTomador;

        // Não enviar campos nulos/vazios.
        foreach (var k in p.Where(kv => kv.Value is null or "").Select(kv => kv.Key).ToList())
            p.Remove(k);
        return p;
    }

    private static NfseResultado Interpretar(HttpStatusCode code, string corpo, bool consulta)
    {
        var n = (int)code;

        if (n >= 500)
            throw new HttpRequestException($"Provedor indisponível (HTTP {n}).", null, code);
        if (code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Token da API de NFS-e inválido ou sem permissão (HTTP " + n + ").");
        if (code == HttpStatusCode.NotFound && consulta)
            return NfseResultado.NaoEncontrada();

        JsonElement? raiz = null;
        try { raiz = JsonDocument.Parse(corpo).RootElement.Clone(); } catch { /* corpo não-JSON */ }

        if (n >= 400)
        {
            return new NfseResultado
            {
                Status = StatusNfse.Rejeitada,
                Mensagem = raiz is { } r ? ExtrairErro(r) : $"HTTP {n}: {corpo}"
            };
        }

        if (raiz is not { } j)
            return new NfseResultado { Status = StatusNfse.Processando };

        var statusBruto = Texto(j, "status") ?? "";
        var status = statusBruto switch
        {
            "autorizada" or "autorizado" => StatusNfse.Autorizada,
            "cancelada" or "cancelado" => StatusNfse.Cancelada,
            "erro_autorizacao" or "rejeitada" or "rejeitado" => StatusNfse.Rejeitada,
            _ => StatusNfse.Processando // "processando_autorizacao" e desconhecidos
        };

        return new NfseResultado
        {
            Status = status,
            Numero = Texto(j, "numero", "numero_nfse"),
            CodigoVerificacao = Texto(j, "codigo_verificacao"),
            // Os nomes de campos de arquivo variam por tipo de nota; tentamos os mais comuns.
            UrlPdf = Texto(j, "url_danfse", "caminho_pdf_nota_fiscal", "url_pdf", "url"),
            UrlXml = Texto(j, "caminho_xml_nota_fiscal", "url_xml", "caminho_xml"),
            Mensagem = status == StatusNfse.Rejeitada ? ExtrairErro(j) : null
        };
    }

    private static string? Texto(JsonElement obj, params string[] nomes)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        foreach (var nome in nomes)
            if (obj.TryGetProperty(nome, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                var s = v.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        return null;
    }

    private static string ExtrairErro(JsonElement j)
    {
        var partes = new List<string>();
        var msg = Texto(j, "mensagem", "mensagem_sefaz", "message");
        if (msg != null) partes.Add(msg);

        if (j.ValueKind == JsonValueKind.Object && j.TryGetProperty("erros", out var erros) && erros.ValueKind == JsonValueKind.Array)
            foreach (var er in erros.EnumerateArray())
                partes.Add(er.ValueKind == JsonValueKind.Object ? (Texto(er, "mensagem", "message") ?? er.ToString()) : er.ToString());

        return partes.Count > 0 ? string.Join(" | ", partes) : "Nota rejeitada pelo provedor.";
    }
}
