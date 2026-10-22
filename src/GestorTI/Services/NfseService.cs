using GestorTI.Data;
using GestorTI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestorTI.Services;

/// <summary>
/// Orquestra a emissão de NFS-e num app local, SEM webhook:
///
///  1. <b>Grava antes de enviar</b>: a nota nasce como PendenteEnvio no SQLite, com uma Referencia única.
///  2. <b>Idempotência</b>: antes de enviar, consulta a referência no provedor; se já existir, só adota o status.
///     Assim, queda de energia/internet no meio do envio nunca gera nota duplicada.
///  3. <b>Sem internet</b>: HttpRequestException não é erro fatal — a nota segue pendente.
///  4. <b>Polling</b>: <see cref="NfsePollingService"/> chama <see cref="SincronizarPendentesAsync"/> periodicamente
///     para (re)enviar pendentes e consultar as "Processando" até autorizar/rejeitar.
///  5. <b>Autorizada</b>: baixa PDF e XML para a pasta configurada no Windows.
/// </summary>
public sealed class NfseService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly INfseProvider _provider;
    private readonly SettingsService _settings;
    private readonly PastasService _pastas;
    private readonly ILogger<NfseService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1); // evita UI e polling mexendo na mesma nota

    public NfseService(IDbContextFactory<AppDbContext> factory, INfseProvider provider,
        SettingsService settings, PastasService pastas, ILogger<NfseService> log)
    {
        _factory = factory; _provider = provider; _settings = settings; _pastas = pastas; _log = log;
    }

    /// <summary>Emite NFS-e para uma cobrança paga. Idempotente: não cria 2ª nota se já houver uma ativa.</summary>
    public async Task<NotaFiscal> EmitirAsync(int cobrancaId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var cobranca = await db.Cobrancas
                .Include(c => c.Cliente).Include(c => c.Notas)
                .FirstOrDefaultAsync(c => c.Id == cobrancaId, ct)
                ?? throw new InvalidOperationException("Cobrança não encontrada.");

            var ativa = cobranca.Notas.FirstOrDefault(n => n.Status != StatusNfse.Rejeitada && n.Status != StatusNfse.Cancelada);
            if (ativa != null) return ativa;

            ValidarConfiguracao(cobranca.Cliente);

            var nota = new NotaFiscal
            {
                CobrancaId = cobranca.Id,
                Referencia = $"gti-{cobranca.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Status = StatusNfse.PendenteEnvio,
                ValorServicos = cobranca.Valor,
                Discriminacao = cobranca.Descricao
            };
            db.Notas.Add(nota);
            await db.SaveChangesAsync(ct); // ← persiste ANTES da rede

            nota.Cobranca = cobranca;
            await ProcessarAsync(db, nota, ct);
            return nota;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Consulta/reenvia uma nota específica agora (botão "Atualizar").</summary>
    public async Task AtualizarAsync(int notaId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var nota = await db.Notas.Include(n => n.Cobranca).ThenInclude(c => c.Cliente)
                .FirstOrDefaultAsync(n => n.Id == notaId, ct)
                ?? throw new InvalidOperationException("Nota não encontrada.");
            await ProcessarAsync(db, nota, ct);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Chamado pelo serviço de background. Retorna quantas notas foram processadas.</summary>
    public async Task<int> SincronizarPendentesAsync(CancellationToken ct = default, bool ignorarIntervalo = false)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var pendentes = await db.Notas
                .Include(n => n.Cobranca).ThenInclude(c => c.Cliente)
                .Where(n => n.Status == StatusNfse.PendenteEnvio || n.Status == StatusNfse.Processando)
                .ToListAsync(ct);

            var agora = DateTime.UtcNow;
            var processadas = 0;
            foreach (var nota in pendentes)
            {
                // Backoff simples: 1 min nas primeiras 10 tentativas, depois 10 min.
                var espera = nota.Tentativas < 10 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(10);
                if (!ignorarIntervalo && nota.UltimaConsulta.HasValue && agora - nota.UltimaConsulta < espera) continue;

                await ProcessarAsync(db, nota, ct);
                processadas++;
            }
            return processadas;
        }
        finally { _gate.Release(); }
    }

    public async Task CancelarAsync(int notaId, string justificativa, CancellationToken ct = default)
    {
        if (justificativa.Trim().Length < 15)
            throw new InvalidOperationException("A justificativa deve ter ao menos 15 caracteres.");

        await _gate.WaitAsync(ct);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var nota = await db.Notas.FirstOrDefaultAsync(n => n.Id == notaId, ct)
                ?? throw new InvalidOperationException("Nota não encontrada.");
            var r = await _provider.CancelarAsync(nota.Referencia, justificativa.Trim(), ct);
            Aplicar(nota, r);
            await db.SaveChangesAsync(ct);
        }
        finally { _gate.Release(); }
    }

    // ------------------------------------------------------------------ núcleo

    private async Task ProcessarAsync(AppDbContext db, NotaFiscal nota, CancellationToken ct)
    {
        try
        {
            // Consulta primeiro: se o provedor já conhece a referência, NÃO reenviamos (idempotência).
            var r = await _provider.ConsultarAsync(nota.Referencia, ct);
            if (!r.Encontrada)
            {
                var emissao = Montar(nota);
                r = await _provider.EmitirAsync(emissao, ct);
            }

            Aplicar(nota, r);
            if (nota.Status == StatusNfse.Autorizada) await SalvarArquivosAsync(nota, r, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Offline/timeout: continua pendente, será tentada de novo pelo polling.
            nota.Mensagem = "Sem conexão com o provedor; nova tentativa automática em instantes.";
            _log.LogWarning(ex, "NFS-e {Ref}: falha de rede", nota.Referencia);
        }
        catch (InvalidOperationException ex)
        {
            // Token inválido etc.: mostra ao usuário, mas não marca como rejeitada.
            nota.Mensagem = ex.Message;
            _log.LogWarning(ex, "NFS-e {Ref}: configuração", nota.Referencia);
        }

        nota.UltimaConsulta = DateTime.UtcNow;
        nota.Tentativas++;
        await db.SaveChangesAsync(ct);
    }

    private static void Aplicar(NotaFiscal nota, NfseResultado r)
    {
        nota.Status = r.Status;
        nota.Numero = r.Numero ?? nota.Numero;
        nota.CodigoVerificacao = r.CodigoVerificacao ?? nota.CodigoVerificacao;
        nota.Mensagem = r.Mensagem;
        if (r.Status == StatusNfse.Autorizada && nota.AutorizadaEm == null) nota.AutorizadaEm = DateTime.UtcNow;
    }

    private async Task SalvarArquivosAsync(NotaFiscal nota, NfseResultado r, CancellationToken ct)
    {
        // Cada cliente tem sua pasta: <PastaNotas>\Clientes\<Nome> - <doc>\
        var pasta = _pastas.Garantir(nota.Cobranca.Cliente);
        var baseNome = $"NFSe-{PastasService.Sanitizar(nota.Numero ?? nota.Referencia)}-{DateTime.Now:yyyyMMdd}";

        // Falha ao baixar não invalida a nota (já autorizada): ficará sem caminho e pode ser rebaixada depois.
        try
        {
            if (nota.CaminhoPdf == null && !string.IsNullOrEmpty(r.UrlPdf))
            {
                var caminho = Path.Combine(pasta, baseNome + ".pdf");
                await File.WriteAllBytesAsync(caminho, await _provider.BaixarAsync(r.UrlPdf, ct), ct);
                nota.CaminhoPdf = caminho;
            }
            if (nota.CaminhoXml == null && !string.IsNullOrEmpty(r.UrlXml))
            {
                var caminho = Path.Combine(pasta, baseNome + ".xml");
                await File.WriteAllBytesAsync(caminho, await _provider.BaixarAsync(r.UrlXml, ct), ct);
                nota.CaminhoXml = caminho;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            _log.LogWarning(ex, "NFS-e {Ref}: não foi possível baixar os arquivos agora", nota.Referencia);
        }
    }

    private NfseEmissao Montar(NotaFiscal nota)
    {
        var s = _settings.Current;
        var cli = nota.Cobranca.Cliente;
        return new NfseEmissao(
            Referencia: nota.Referencia,
            DataEmissao: DateTimeOffset.Now,
            Competencia: nota.Cobranca.Competencia ?? nota.Cobranca.Vencimento,
            CnpjPrestador: Util.SomenteDigitos(s.CnpjPrestador),
            InscricaoMunicipal: s.InscricaoMunicipal,
            CodigoMunicipioEmissor: s.CodigoMunicipioEmissor,
            CodigoOpcaoSimplesNacional: s.CodigoOpcaoSimplesNacional,
            CodigoTributacaoNacional: s.CodigoTributacaoNacional,
            CodigoNbs: s.CodigoNbs,
            Descricao: nota.Discriminacao,
            Valor: nota.ValorServicos,
            DocumentoTomador: cli.CpfCnpj,
            NomeTomador: cli.Nome,
            EmailTomador: cli.Email,
            TelefoneTomador: Util.SomenteDigitos(cli.Telefone),
            CepTomador: Util.SomenteDigitos(cli.Cep),
            LogradouroTomador: cli.Logradouro,
            NumeroTomador: cli.Numero,
            BairroTomador: cli.Bairro,
            CodigoMunicipioTomador: cli.CodigoMunicipioIbge);
    }

    private void ValidarConfiguracao(Cliente cliente)
    {
        var s = _settings.Current;
        if (string.IsNullOrWhiteSpace(_settings.FocusToken))
            throw new InvalidOperationException("Informe o token da API de NFS-e em Configurações.");
        if (Util.SomenteDigitos(s.CnpjPrestador).Length != 14)
            throw new InvalidOperationException("Informe o CNPJ do prestador (MEI) em Configurações.");
        if (Util.SomenteDigitos(s.CodigoMunicipioEmissor).Length != 7)
            throw new InvalidOperationException("Informe o código IBGE (7 dígitos) do município emissor em Configurações.");
        if (string.IsNullOrWhiteSpace(cliente.CodigoMunicipioIbge))
            throw new InvalidOperationException($"Cadastre o código IBGE do município do cliente \"{cliente.Nome}\".");
    }
}
