using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Data;
using GestorTI.Models;
using GestorTI.Services;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.ViewModels;

public partial class ContratosViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly CobrancaService _cobrancas;

    public ContratosViewModel(IDbContextFactory<AppDbContext> factory, CobrancaService cobrancas)
    {
        _factory = factory;
        _cobrancas = cobrancas;
    }

    public ObservableCollection<Contrato> Contratos { get; } = new();
    public ObservableCollection<Cliente> Clientes { get; } = new();
    public Array Tipos { get; } = Enum.GetValues(typeof(TipoServico));
    public Array Periodicidades { get; } = Enum.GetValues(typeof(Periodicidade));

    // Formulário
    [ObservableProperty] private Cliente? _clienteSelecionado;
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private TipoServico _tipoSelecionado = TipoServico.SuporteTecnico;
    [ObservableProperty] private Periodicidade _periodicidadeSelecionada = Periodicidade.Mensal;
    [ObservableProperty] private decimal? _valor;
    [ObservableProperty] private decimal? _slaHoras = 8;
    [ObservableProperty] private string _slaObservacoes = "";
    [ObservableProperty] private DateTime? _dataInicio = DateTime.Today;
    [ObservableProperty] private DateTime? _dataFim;
    [ObservableProperty] private decimal? _diaVencimento = 10;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemVencendo))]
    private int _vencendo;

    public bool TemVencendo => Vencendo > 0;

    public override Task CarregarAsync() => SafeAsync(RecarregarAsync);

    private async Task RecarregarAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var contratos = await db.Contratos.AsNoTracking().Include(c => c.Cliente).ToListAsync();
        var clientes = await db.Clientes.AsNoTracking().Where(c => c.Ativo).OrderBy(c => c.Nome).ToListAsync();

        Contratos.Clear();
        foreach (var c in contratos.OrderBy(c => c.Status).ThenBy(c => c.Cliente.Nome)) Contratos.Add(c);

        Clientes.Clear();
        foreach (var c in clientes) Clientes.Add(c);

        Vencendo = contratos.Count(c => c.Status == StatusContrato.Ativo && c.DiasParaVencer is <= 60);
    }

    [RelayCommand]
    private Task SalvarAsync() => SafeAsync(async () =>
    {
        if (ClienteSelecionado is null) throw new InvalidOperationException("Selecione o cliente.");
        if (string.IsNullOrWhiteSpace(Descricao)) throw new InvalidOperationException("Descreva o serviço contratado.");
        if (Valor is null or <= 0) throw new InvalidOperationException("Informe um valor maior que zero.");
        if (DataInicio is null) throw new InvalidOperationException("Informe a data de início.");
        if (DataFim is { } fim && fim < DataInicio) throw new InvalidOperationException("O fim da vigência é anterior ao início.");

        await using var db = await _factory.CreateDbContextAsync();
        db.Contratos.Add(new Contrato
        {
            ClienteId = ClienteSelecionado.Id,
            Descricao = Descricao.Trim(),
            Tipo = TipoSelecionado,
            Periodicidade = PeriodicidadeSelecionada,
            Valor = Valor.Value,
            SlaHorasResposta = (int)(SlaHoras ?? 8),
            SlaObservacoes = SlaObservacoes.Trim(),
            DataInicio = DataInicio.Value.Date,
            DataFim = DataFim?.Date,
            DiaVencimento = (int)Math.Clamp(DiaVencimento ?? 10, 1, 31)
        });
        await db.SaveChangesAsync();

        Descricao = SlaObservacoes = "";
        Valor = null;
        DataFim = null;
        await RecarregarAsync();
        Mensagem = "Contrato criado. As cobranças são geradas automaticamente a cada mês.";
    });

    [RelayCommand]
    private Task EncerrarAsync(Contrato? contrato) => SafeAsync(async () =>
    {
        if (contrato is null) return;
        await using var db = await _factory.CreateDbContextAsync();
        var c = await db.Contratos.FirstOrDefaultAsync(x => x.Id == contrato.Id)
                ?? throw new InvalidOperationException("Contrato não encontrado.");
        c.Status = StatusContrato.Encerrado;
        await db.SaveChangesAsync();
        await RecarregarAsync();
        Mensagem = "Contrato encerrado.";
    });

    [RelayCommand]
    private Task GerarCobrancasAsync() => SafeAsync(async () =>
    {
        var n = await _cobrancas.GerarRecorrentesAsync(DateTime.Today);
        Mensagem = n == 0 ? "Nenhuma cobrança nova para este mês." : $"{n} cobrança(s) gerada(s) para este mês.";
    });
}
