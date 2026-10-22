using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Data;
using GestorTI.Models;
using GestorTI.Services;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.ViewModels;

public partial class FinanceiroViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly CobrancaService _cobrancas;

    public FinanceiroViewModel(IDbContextFactory<AppDbContext> factory, CobrancaService cobrancas)
    {
        _factory = factory;
        _cobrancas = cobrancas;
    }

    public ObservableCollection<Cobranca> Cobrancas { get; } = new();
    public ObservableCollection<LancamentoCaixa> Lancamentos { get; } = new();
    public ObservableCollection<Cliente> Clientes { get; } = new();

    public string[] Filtros { get; } = { "Todas", "Pendentes", "Vencidas", "Pagas", "Canceladas" };
    public Array TiposLancamento { get; } = Enum.GetValues(typeof(TipoLancamento));

    [ObservableProperty] private string _filtroSelecionado = "Todas";

    // Nova cobrança avulsa
    [ObservableProperty] private Cliente? _clienteSelecionado;
    [ObservableProperty] private string _descricaoCobranca = "";
    [ObservableProperty] private decimal? _valorCobranca;
    [ObservableProperty] private DateTime? _vencimentoCobranca = DateTime.Today.AddDays(7);

    // Novo lançamento de caixa
    [ObservableProperty] private TipoLancamento _tipoLancamento = TipoLancamento.Saida;
    [ObservableProperty] private string _descricaoLancamento = "";
    [ObservableProperty] private string _categoriaLancamento = "";
    [ObservableProperty] private decimal? _valorLancamento;
    [ObservableProperty] private DateTime? _dataLancamento = DateTime.Today;

    partial void OnFiltroSelecionadoChanged(string value) => _ = CarregarAsync();

    public override Task CarregarAsync() => SafeAsync(RecarregarAsync);

    private async Task RecarregarAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var todas = await db.Cobrancas.AsNoTracking().Include(c => c.Cliente).ToListAsync();
        var lancs = await db.Lancamentos.AsNoTracking().ToListAsync();
        var clientes = await db.Clientes.AsNoTracking().Where(c => c.Ativo).OrderBy(c => c.Nome).ToListAsync();

        IEnumerable<Cobranca> filtradas = FiltroSelecionado switch
        {
            "Pendentes" => todas.Where(c => c.Status == StatusCobranca.Pendente && !c.EstaVencida),
            "Vencidas" => todas.Where(c => c.EstaVencida),
            "Pagas" => todas.Where(c => c.Status == StatusCobranca.Paga),
            "Canceladas" => todas.Where(c => c.Status == StatusCobranca.Cancelada),
            _ => todas
        };

        Cobrancas.Clear();
        foreach (var c in filtradas.OrderByDescending(c => c.Vencimento).Take(300)) Cobrancas.Add(c);

        Lancamentos.Clear();
        foreach (var l in lancs.OrderByDescending(l => l.Data).ThenByDescending(l => l.Id).Take(200)) Lancamentos.Add(l);

        Clientes.Clear();
        foreach (var c in clientes) Clientes.Add(c);
    }

    [RelayCommand]
    private Task ReceberAsync(Cobranca? cobranca) => SafeAsync(async () =>
    {
        if (cobranca is null) return;
        await _cobrancas.ReceberAsync(cobranca.Id, FormaPagamento.Pix);
        await RecarregarAsync();
        Mensagem = "Recebimento registrado e lançado no caixa.";
    });

    [RelayCommand]
    private Task CancelarAsync(Cobranca? cobranca) => SafeAsync(async () =>
    {
        if (cobranca is null) return;
        await using var db = await _factory.CreateDbContextAsync();
        var c = await db.Cobrancas.FirstOrDefaultAsync(x => x.Id == cobranca.Id)
                ?? throw new InvalidOperationException("Cobrança não encontrada.");
        if (c.Status != StatusCobranca.Pendente)
            throw new InvalidOperationException("Somente cobranças pendentes podem ser canceladas.");
        c.Status = StatusCobranca.Cancelada;
        await db.SaveChangesAsync();
        await RecarregarAsync();
        Mensagem = "Cobrança cancelada.";
    });

    [RelayCommand]
    private Task NovaCobrancaAsync() => SafeAsync(async () =>
    {
        if (ClienteSelecionado is null) throw new InvalidOperationException("Selecione o cliente.");
        if (string.IsNullOrWhiteSpace(DescricaoCobranca)) throw new InvalidOperationException("Informe a descrição.");
        if (ValorCobranca is null or <= 0) throw new InvalidOperationException("Informe um valor maior que zero.");
        if (VencimentoCobranca is null) throw new InvalidOperationException("Informe o vencimento.");

        await using var db = await _factory.CreateDbContextAsync();
        db.Cobrancas.Add(new Cobranca
        {
            ClienteId = ClienteSelecionado.Id,
            Descricao = DescricaoCobranca.Trim(),
            Valor = ValorCobranca.Value,
            Vencimento = VencimentoCobranca.Value.Date
        });
        await db.SaveChangesAsync();

        DescricaoCobranca = "";
        ValorCobranca = null;
        await RecarregarAsync();
        Mensagem = "Cobrança criada.";
    });

    [RelayCommand]
    private Task NovoLancamentoAsync() => SafeAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(DescricaoLancamento)) throw new InvalidOperationException("Informe a descrição.");
        if (ValorLancamento is null or <= 0) throw new InvalidOperationException("Informe um valor maior que zero.");

        await using var db = await _factory.CreateDbContextAsync();
        db.Lancamentos.Add(new LancamentoCaixa
        {
            Tipo = TipoLancamento,
            Descricao = DescricaoLancamento.Trim(),
            Categoria = CategoriaLancamento.Trim(),
            Valor = ValorLancamento.Value,
            Data = (DataLancamento ?? DateTime.Today).Date
        });
        await db.SaveChangesAsync();

        DescricaoLancamento = CategoriaLancamento = "";
        ValorLancamento = null;
        await RecarregarAsync();
        Mensagem = "Lançamento registrado.";
    });
}
