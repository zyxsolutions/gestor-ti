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

    // ---------------- Formulário de cobrança (nova ou edição) ----------------
    [ObservableProperty] private Cliente? _clienteSelecionado;
    [ObservableProperty] private string _descricaoCobranca = "";
    [ObservableProperty] private decimal? _valorCobranca;
    [ObservableProperty] private DateTime? _vencimentoCobranca = DateTime.Today.AddDays(7);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditandoCobranca))]
    [NotifyPropertyChangedFor(nameof(TituloFormCobranca))]
    [NotifyPropertyChangedFor(nameof(TextoBotaoCobranca))]
    private Cobranca? _cobrancaEmEdicao;

    public bool EditandoCobranca => CobrancaEmEdicao is not null;
    public string TituloFormCobranca => EditandoCobranca ? "Editar cobrança" : "Nova cobrança avulsa";
    public string TextoBotaoCobranca => EditandoCobranca ? "Salvar alterações" : "Criar cobrança";

    // ---------------- Formulário de lançamento (novo ou edição) ----------------
    [ObservableProperty] private TipoLancamento _tipoLancamento = TipoLancamento.Saida;
    [ObservableProperty] private string _descricaoLancamento = "";
    [ObservableProperty] private string _categoriaLancamento = "";
    [ObservableProperty] private decimal? _valorLancamento;
    [ObservableProperty] private DateTime? _dataLancamento = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditandoLancamento))]
    [NotifyPropertyChangedFor(nameof(TituloFormLancamento))]
    [NotifyPropertyChangedFor(nameof(TextoBotaoLancamento))]
    private LancamentoCaixa? _lancamentoEmEdicao;

    public bool EditandoLancamento => LancamentoEmEdicao is not null;
    public string TituloFormLancamento => EditandoLancamento ? "Editar lançamento" : "Novo lançamento";
    public string TextoBotaoLancamento => EditandoLancamento ? "Salvar alterações" : "Registrar";

    // ---------------- Confirmação de exclusão ----------------
    private Func<Task>? _acaoPendente;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemConfirmacao))]
    private string _textoConfirmacao = "";

    public bool TemConfirmacao => !string.IsNullOrEmpty(TextoConfirmacao);

    private void PedirConfirmacao(string texto, Func<Task> acao)
    {
        _acaoPendente = acao;
        TextoConfirmacao = texto;
    }

    private void LimparConfirmacao()
    {
        _acaoPendente = null;
        TextoConfirmacao = "";
    }

    [RelayCommand]
    private Task ConfirmarAsync() => SafeAsync(async () =>
    {
        var acao = _acaoPendente;
        LimparConfirmacao();
        if (acao is not null) await acao();
    });

    [RelayCommand]
    private void CancelarConfirmacao() => LimparConfirmacao();

    // ---------------- Carga ----------------
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

    // ---------------- Cobranças ----------------
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

    /// <summary>Carrega a cobrança no formulário para edição.</summary>
    [RelayCommand]
    private void EditarCobranca(Cobranca? cobranca)
    {
        if (cobranca is null) return;
        if (!cobranca.PodeEditar) { Mensagem = "Cobranças canceladas não podem ser editadas."; return; }

        ClienteSelecionado = Clientes.FirstOrDefault(c => c.Id == cobranca.ClienteId);
        DescricaoCobranca = cobranca.Descricao;
        ValorCobranca = cobranca.Valor;
        VencimentoCobranca = cobranca.Vencimento;
        CobrancaEmEdicao = cobranca;
        Mensagem = null;
    }

    [RelayCommand]
    private void CancelarEdicaoCobranca() => LimparFormCobranca();

    private void LimparFormCobranca()
    {
        CobrancaEmEdicao = null;
        DescricaoCobranca = "";
        ValorCobranca = null;
        VencimentoCobranca = DateTime.Today.AddDays(7);
    }

    /// <summary>Cria uma cobrança avulsa ou, se há uma em edição, salva as alterações.</summary>
    [RelayCommand]
    private Task SalvarCobrancaAsync() => SafeAsync(async () =>
    {
        if (ClienteSelecionado is null) throw new InvalidOperationException("Selecione o cliente.");
        if (string.IsNullOrWhiteSpace(DescricaoCobranca)) throw new InvalidOperationException("Informe a descrição.");
        if (ValorCobranca is null or <= 0) throw new InvalidOperationException("Informe um valor maior que zero.");
        if (VencimentoCobranca is null) throw new InvalidOperationException("Informe o vencimento.");

        await using var db = await _factory.CreateDbContextAsync();

        if (CobrancaEmEdicao is null)
        {
            db.Cobrancas.Add(new Cobranca
            {
                ClienteId = ClienteSelecionado.Id,
                Descricao = DescricaoCobranca.Trim(),
                Valor = ValorCobranca.Value,
                Vencimento = VencimentoCobranca.Value.Date
            });
            await db.SaveChangesAsync();
            LimparFormCobranca();
            await RecarregarAsync();
            Mensagem = "Cobrança criada.";
            return;
        }

        var c = await db.Cobrancas.Include(x => x.Notas)
                    .FirstOrDefaultAsync(x => x.Id == CobrancaEmEdicao.Id)
                ?? throw new InvalidOperationException("Cobrança não encontrada (foi excluída?).");
        if (c.Status == StatusCobranca.Cancelada)
            throw new InvalidOperationException("Cobranças canceladas não podem ser editadas.");

        var valorMudou = c.Valor != ValorCobranca.Value;
        var temNotaAtiva = c.Notas.Any(n => n.Status != StatusNfse.Rejeitada && n.Status != StatusNfse.Cancelada);
        if (valorMudou && temNotaAtiva)
            throw new InvalidOperationException(
                "Esta cobrança já tem NFS-e emitida/em andamento: o valor não pode mudar. Cancele a nota antes.");

        c.ClienteId = ClienteSelecionado.Id;
        c.Descricao = DescricaoCobranca.Trim();
        c.Valor = ValorCobranca.Value;
        c.Vencimento = VencimentoCobranca.Value.Date;

        // Cobrança já paga: mantém o caixa coerente com o novo valor/descrição.
        if (c.Status == StatusCobranca.Paga)
        {
            var lancs = await db.Lancamentos.Where(l => l.CobrancaId == c.Id).ToListAsync();
            foreach (var l in lancs)
            {
                l.Valor = c.Valor;
                l.Descricao = $"Recebimento: {c.Descricao}";
            }
        }

        await db.SaveChangesAsync();
        LimparFormCobranca();
        await RecarregarAsync();
        Mensagem = "Cobrança atualizada.";
    });

    /// <summary>Pede confirmação antes de excluir.</summary>
    [RelayCommand]
    private void ExcluirCobranca(Cobranca? cobranca)
    {
        if (cobranca is null) return;
        var id = cobranca.Id;
        PedirConfirmacao(
            $"Excluir a cobrança \"{cobranca.Descricao}\" ({cobranca.Valor:C})? Esta ação não pode ser desfeita.",
            () => ExcluirCobrancaConfirmadaAsync(id));
    }

    private async Task ExcluirCobrancaConfirmadaAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var c = await db.Cobrancas.Include(x => x.Notas).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new InvalidOperationException("Cobrança não encontrada.");

        if (c.Notas.Any(n => n.Status != StatusNfse.Rejeitada && n.Status != StatusNfse.Cancelada))
            throw new InvalidOperationException(
                "Esta cobrança possui NFS-e emitida/em andamento e não pode ser excluída. Cancele a nota antes.");

        // Estorna o caixa: some a entrada gerada pelo recebimento.
        var lancs = await db.Lancamentos.Where(l => l.CobrancaId == c.Id).ToListAsync();
        db.Lancamentos.RemoveRange(lancs);

        string mensagem;
        if (c.ContratoId is not null)
        {
            // Cobrança gerada por contrato: se fosse apagada, a rotina mensal a recriaria.
            // Fica como "Cancelada" (histórico) — isso impede a recriação.
            if (c.Status == StatusCobranca.Cancelada && lancs.Count == 0)
                throw new InvalidOperationException(
                    "Cobrança de contrato já está cancelada. Para parar de gerar, encerre o contrato em Assinaturas.");
            c.Status = StatusCobranca.Cancelada;
            c.DataPagamento = null;
            mensagem = "Cobrança de contrato cancelada (não é apagada para a rotina mensal não recriá-la). " +
                       "Para parar de gerar, encerre o contrato em Assinaturas.";
        }
        else
        {
            db.Notas.RemoveRange(c.Notas); // só restam rejeitadas/canceladas
            db.Cobrancas.Remove(c);
            mensagem = "Cobrança excluída.";
        }

        await db.SaveChangesAsync();
        if (CobrancaEmEdicao?.Id == id) LimparFormCobranca();
        await RecarregarAsync();
        Mensagem = mensagem;
    }

    // ---------------- Lançamentos de caixa ----------------
    [RelayCommand]
    private void EditarLancamento(LancamentoCaixa? l)
    {
        if (l is null) return;
        if (!l.PodeEditar)
        {
            Mensagem = "Este lançamento veio do recebimento de uma cobrança. Altere ou exclua a cobrança.";
            return;
        }

        TipoLancamento = l.Tipo;
        DescricaoLancamento = l.Descricao;
        CategoriaLancamento = l.Categoria ?? "";
        ValorLancamento = l.Valor;
        DataLancamento = l.Data;
        LancamentoEmEdicao = l;
        Mensagem = null;
    }

    [RelayCommand]
    private void CancelarEdicaoLancamento() => LimparFormLancamento();

    private void LimparFormLancamento()
    {
        LancamentoEmEdicao = null;
        DescricaoLancamento = CategoriaLancamento = "";
        ValorLancamento = null;
        DataLancamento = DateTime.Today;
    }

    /// <summary>Registra um lançamento novo ou, se há um em edição, salva as alterações.</summary>
    [RelayCommand]
    private Task SalvarLancamentoAsync() => SafeAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(DescricaoLancamento)) throw new InvalidOperationException("Informe a descrição.");
        if (ValorLancamento is null or <= 0) throw new InvalidOperationException("Informe um valor maior que zero.");

        await using var db = await _factory.CreateDbContextAsync();

        if (LancamentoEmEdicao is null)
        {
            db.Lancamentos.Add(new LancamentoCaixa
            {
                Tipo = TipoLancamento,
                Descricao = DescricaoLancamento.Trim(),
                Categoria = CategoriaLancamento.Trim(),
                Valor = ValorLancamento.Value,
                Data = (DataLancamento ?? DateTime.Today).Date
            });
            await db.SaveChangesAsync();
            LimparFormLancamento();
            await RecarregarAsync();
            Mensagem = "Lançamento registrado.";
            return;
        }

        var l = await db.Lancamentos.FirstOrDefaultAsync(x => x.Id == LancamentoEmEdicao.Id)
                ?? throw new InvalidOperationException("Lançamento não encontrado (foi excluído?).");
        if (l.CobrancaId is not null)
            throw new InvalidOperationException("Este lançamento veio de uma cobrança: altere a cobrança.");

        l.Tipo = TipoLancamento;
        l.Descricao = DescricaoLancamento.Trim();
        l.Categoria = CategoriaLancamento.Trim();
        l.Valor = ValorLancamento.Value;
        l.Data = (DataLancamento ?? DateTime.Today).Date;
        await db.SaveChangesAsync();

        LimparFormLancamento();
        await RecarregarAsync();
        Mensagem = "Lançamento atualizado.";
    });

    [RelayCommand]
    private void ExcluirLancamento(LancamentoCaixa? l)
    {
        if (l is null) return;
        if (!l.PodeEditar)
        {
            Mensagem = "Este lançamento veio do recebimento de uma cobrança. Exclua a cobrança para estornar.";
            return;
        }

        var id = l.Id;
        PedirConfirmacao(
            $"Excluir o lançamento \"{l.Descricao}\" ({l.Valor:C})? Esta ação não pode ser desfeita.",
            async () =>
            {
                await using var db = await _factory.CreateDbContextAsync();
                var alvo = await db.Lancamentos.FirstOrDefaultAsync(x => x.Id == id)
                           ?? throw new InvalidOperationException("Lançamento não encontrado.");
                if (alvo.CobrancaId is not null)
                    throw new InvalidOperationException("Este lançamento veio de uma cobrança.");
                db.Lancamentos.Remove(alvo);
                await db.SaveChangesAsync();
                if (LancamentoEmEdicao?.Id == id) LimparFormLancamento();
                await RecarregarAsync();
                Mensagem = "Lançamento excluído.";
            });
    }
}
