using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Data;
using GestorTI.Models;
using GestorTI.Services;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.ViewModels;

public partial class NotasFiscaisViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly NfseService _nfse;
    private readonly SettingsService _settings;
    private readonly PastasService _pastas;

    public NotasFiscaisViewModel(IDbContextFactory<AppDbContext> factory, NfseService nfse,
        SettingsService settings, PastasService pastas)
    {
        _factory = factory;
        _nfse = nfse;
        _settings = settings;
        _pastas = pastas;
    }

    [ObservableProperty] private bool _semProntas = true;

    /// <summary>Cobranças pagas que ainda não têm NFS-e ativa.</summary>
    public ObservableCollection<Cobranca> ProntasParaEmitir { get; } = new();
    public ObservableCollection<NotaFiscal> Notas { get; } = new();

    // ---- Pasta por cliente ----
    public ObservableCollection<Cliente> Clientes { get; } = new();
    public ObservableCollection<ArquivoCliente> ArquivosCliente { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemClienteSelecionado))]
    private Cliente? _clienteSelecionado;

    [ObservableProperty] private string _caminhoPastaCliente = "";
    [ObservableProperty] private bool _semArquivos = true;

    public bool TemClienteSelecionado => ClienteSelecionado is not null;

    partial void OnClienteSelecionadoChanged(Cliente? value) => AtualizarArquivos();

    private void AtualizarArquivos()
    {
        ArquivosCliente.Clear();
        if (ClienteSelecionado is null)
        {
            CaminhoPastaCliente = "";
            SemArquivos = true;
            return;
        }

        CaminhoPastaCliente = _pastas.Existe(ClienteSelecionado)
            ? _pastas.CaminhoDoCliente(ClienteSelecionado)
            : "Pasta ainda não criada — clique em \"Criar pasta do cliente\".";

        foreach (var a in _pastas.Listar(ClienteSelecionado)) ArquivosCliente.Add(a);
        SemArquivos = ArquivosCliente.Count == 0;
    }

    public override Task CarregarAsync() => SafeAsync(RecarregarAsync);

    private async Task RecarregarAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var prontas = await db.Cobrancas.AsNoTracking().Include(c => c.Cliente)
            .Where(c => c.Status == StatusCobranca.Paga &&
                        !c.Notas.Any(n => n.Status != StatusNfse.Rejeitada && n.Status != StatusNfse.Cancelada))
            .ToListAsync();

        var notas = await db.Notas.AsNoTracking()
            .Include(n => n.Cobranca).ThenInclude(c => c.Cliente)
            .ToListAsync();

        var clientes = await db.Clientes.AsNoTracking().OrderBy(c => c.Nome).ToListAsync();

        ProntasParaEmitir.Clear();
        foreach (var c in prontas.OrderBy(c => c.DataPagamento)) ProntasParaEmitir.Add(c);
        SemProntas = ProntasParaEmitir.Count == 0;

        Notas.Clear();
        foreach (var n in notas.OrderByDescending(n => n.CriadaEm)) Notas.Add(n);

        // Recarrega a lista de clientes mantendo a seleção atual (por Id).
        var idSelecionado = ClienteSelecionado?.Id;
        Clientes.Clear();
        foreach (var c in clientes) Clientes.Add(c);
        ClienteSelecionado = Clientes.FirstOrDefault(c => c.Id == idSelecionado);
        AtualizarArquivos();
    }

    // ------------------------------------------------------------------ pasta do cliente

    [RelayCommand]
    private Task CriarPastaClienteAsync() => SafeAsync(() =>
    {
        var cliente = ClienteSelecionado ?? throw new InvalidOperationException("Selecione o cliente.");
        var existia = _pastas.Existe(cliente);
        var pasta = _pastas.Garantir(cliente);
        AtualizarArquivos();
        Mensagem = existia ? "A pasta deste cliente já existe: " + pasta : "Pasta criada: " + pasta;
        return Task.CompletedTask;
    });

    [RelayCommand]
    private void AbrirPastaCliente()
    {
        if (ClienteSelecionado is null) { Mensagem = "Selecione o cliente."; return; }
        // Abre (e cria, se preciso) — o usuário já está pedindo para ver a pasta.
        Abrir(_pastas.Garantir(ClienteSelecionado));
        AtualizarArquivos();
    }

    /// <summary>Chamado pela View depois que o usuário escolhe os arquivos no seletor do Windows.</summary>
    public Task ImportarArquivosAsync(IReadOnlyList<string> caminhos) => SafeAsync(() =>
    {
        var cliente = ClienteSelecionado ?? throw new InvalidOperationException("Selecione o cliente antes de importar.");
        if (caminhos.Count == 0) return Task.CompletedTask;

        var copiados = _pastas.Importar(cliente, caminhos);
        AtualizarArquivos();
        Mensagem = copiados.Count == 0
            ? "Nenhum arquivo copiado."
            : $"{copiados.Count} arquivo(s) copiado(s) para a pasta de {cliente.Nome}.";
        return Task.CompletedTask;
    });

    [RelayCommand]
    private void AbrirArquivo(ArquivoCliente? arquivo) => Abrir(arquivo?.Caminho);

    // ------------------------------------------------------------------ emissão

    [RelayCommand]
    private Task EmitirAsync(Cobranca? cobranca) => SafeAsync(async () =>
    {
        if (cobranca is null) return;
        var nota = await _nfse.EmitirAsync(cobranca.Id);
        await RecarregarAsync();
        Mensagem = nota.Status switch
        {
            StatusNfse.Autorizada => "NFS-e autorizada e arquivos salvos na pasta do cliente.",
            StatusNfse.Rejeitada => "NFS-e rejeitada: " + nota.Mensagem,
            StatusNfse.PendenteEnvio => "Nota registrada. " + (nota.Mensagem ?? "Será enviada automaticamente."),
            _ => "NFS-e enviada. O status é atualizado automaticamente a cada minuto."
        };
    });

    [RelayCommand]
    private Task AtualizarAsync(NotaFiscal? nota) => SafeAsync(async () =>
    {
        if (nota is null) return;
        await _nfse.AtualizarAsync(nota.Id);
        await RecarregarAsync();
    });

    [RelayCommand]
    private Task SincronizarAsync() => SafeAsync(async () =>
    {
        var n = await _nfse.SincronizarPendentesAsync(ignorarIntervalo: true);
        await RecarregarAsync();
        Mensagem = n == 0 ? "Nenhuma nota pendente." : $"{n} nota(s) sincronizada(s).";
    });

    [RelayCommand]
    private void AbrirPdf(NotaFiscal? nota) => Abrir(nota?.CaminhoPdf);

    [RelayCommand]
    private void AbrirXml(NotaFiscal? nota) => Abrir(nota?.CaminhoXml);

    [RelayCommand]
    private void AbrirPasta()
    {
        var pasta = _settings.Current.PastaNotas;
        Directory.CreateDirectory(pasta);
        Abrir(pasta);
    }

    private void Abrir(string? caminho)
    {
        if (string.IsNullOrEmpty(caminho)) return;
        try { Sistema.Abrir(caminho); }
        catch (Exception ex) { Mensagem = "Não foi possível abrir: " + ex.Message; }
    }
}
