using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Data;
using GestorTI.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.ViewModels;

/// <summary>Serviços avulsos: cliente, valor, o que será feito (anotação) e geração da cobrança.</summary>
public partial class ServicosViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ServicosViewModel(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public ObservableCollection<Servico> Servicos { get; } = new();
    public ObservableCollection<Cliente> Clientes { get; } = new();

    public string[] Categorias { get; } =
    {
        "Desenvolvimento de sistema", "Criação de site", "Infraestrutura e redes",
        "Suporte técnico", "Consultoria", "Manutenção", "Outro"
    };

    // Formulário
    [ObservableProperty] private Cliente? _clienteSelecionado;
    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _categoriaSelecionada = "Outro";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private decimal? _valor;
    [ObservableProperty] private DateTime? _dataInicio = DateTime.Today;
    [ObservableProperty] private DateTime? _vencimentoCobranca = DateTime.Today.AddDays(7);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editando))]
    [NotifyPropertyChangedFor(nameof(TituloForm))]
    [NotifyPropertyChangedFor(nameof(TextoBotao))]
    private Servico? _servicoEmEdicao;

    public bool Editando => ServicoEmEdicao is not null;
    public string TituloForm => Editando ? "Editar serviço" : "Novo serviço";
    public string TextoBotao => Editando ? "Salvar alterações" : "Cadastrar serviço";

    // Confirmação de exclusão
    private Func<Task>? _acaoPendente;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemConfirmacao))]
    private string _textoConfirmacao = "";
    public bool TemConfirmacao => !string.IsNullOrEmpty(TextoConfirmacao);

    [RelayCommand]
    private Task ConfirmarAsync() => SafeAsync(async () =>
    {
        var acao = _acaoPendente; _acaoPendente = null; TextoConfirmacao = "";
        if (acao is not null) await acao();
    });

    [RelayCommand]
    private void CancelarConfirmacao() { _acaoPendente = null; TextoConfirmacao = ""; }

    public override Task CarregarAsync() => SafeAsync(RecarregarAsync);

    private async Task RecarregarAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var servicos = await db.Servicos.AsNoTracking().Include(s => s.Cliente).ToListAsync();
        var clientes = await db.Clientes.AsNoTracking().Where(c => c.Ativo).OrderBy(c => c.Nome).ToListAsync();

        Servicos.Clear();
        foreach (var s in servicos.OrderBy(s => s.Status).ThenByDescending(s => s.DataInicio)) Servicos.Add(s);
        Clientes.Clear();
        foreach (var c in clientes) Clientes.Add(c);
    }

    private void LimparForm()
    {
        ServicoEmEdicao = null;
        ClienteSelecionado = null;
        Titulo = Descricao = "";
        CategoriaSelecionada = "Outro";
        Valor = null;
        DataInicio = DateTime.Today;
        VencimentoCobranca = DateTime.Today.AddDays(7);
    }

    [RelayCommand]
    private Task SalvarAsync() => SafeAsync(async () =>
    {
        if (ClienteSelecionado is null) throw new InvalidOperationException("Selecione o cliente.");
        if (string.IsNullOrWhiteSpace(Titulo)) throw new InvalidOperationException("Informe o título do serviço.");
        if (Valor is null or <= 0) throw new InvalidOperationException("Informe um valor maior que zero.");
        if (DataInicio is null) throw new InvalidOperationException("Informe a data de início.");

        await using var db = await _factory.CreateDbContextAsync();
        Servico s;
        if (ServicoEmEdicao is null)
        {
            s = new Servico();
            db.Servicos.Add(s);
        }
        else
        {
            s = await db.Servicos.FirstOrDefaultAsync(x => x.Id == ServicoEmEdicao.Id)
                ?? throw new InvalidOperationException("Serviço não encontrado.");
        }

        s.ClienteId = ClienteSelecionado.Id;
        s.Titulo = Titulo.Trim();
        s.Categoria = CategoriaSelecionada;
        s.Descricao = string.IsNullOrWhiteSpace(Descricao) ? null : Descricao.Trim();
        s.Valor = Valor.Value;
        s.DataInicio = DataInicio.Value.Date;
        await db.SaveChangesAsync();

        var criou = !Editando;
        LimparForm();
        await RecarregarAsync();
        Mensagem = criou ? "Serviço cadastrado." : "Serviço atualizado.";
    });

    [RelayCommand]
    private void EditarServico(Servico? s)
    {
        if (s is null) return;
        ClienteSelecionado = Clientes.FirstOrDefault(c => c.Id == s.ClienteId);
        Titulo = s.Titulo;
        CategoriaSelecionada = Categorias.Contains(s.Categoria) ? s.Categoria : "Outro";
        Descricao = s.Descricao ?? "";
        Valor = s.Valor;
        DataInicio = s.DataInicio;
        ServicoEmEdicao = s;
        Mensagem = null;
    }

    [RelayCommand]
    private void CancelarEdicao() => LimparForm();

    private async Task MudarStatusAsync(Servico? s, StatusServico novo, string msg)
    {
        if (s is null) return;
        await using var db = await _factory.CreateDbContextAsync();
        var x = await db.Servicos.FirstOrDefaultAsync(v => v.Id == s.Id)
                ?? throw new InvalidOperationException("Serviço não encontrado.");
        x.Status = novo;
        await db.SaveChangesAsync();
        await RecarregarAsync();
        Mensagem = msg;
    }

    [RelayCommand]
    private Task ConcluirAsync(Servico? s) => SafeAsync(() => MudarStatusAsync(s, StatusServico.Concluido, "Serviço concluído."));

    [RelayCommand]
    private Task CancelarServicoAsync(Servico? s) => SafeAsync(() => MudarStatusAsync(s, StatusServico.Cancelado, "Serviço cancelado."));

    [RelayCommand]
    private Task GerarCobrancaAsync(Servico? s) => SafeAsync(async () =>
    {
        if (s is null) return;
        await using var db = await _factory.CreateDbContextAsync();
        var x = await db.Servicos.FirstOrDefaultAsync(v => v.Id == s.Id)
                ?? throw new InvalidOperationException("Serviço não encontrado.");
        if (x.CobrancaId is not null) throw new InvalidOperationException("Este serviço já tem cobrança.");

        var cobranca = new Cobranca
        {
            ClienteId = x.ClienteId,
            Descricao = x.Titulo,
            Valor = x.Valor,
            Vencimento = (VencimentoCobranca ?? DateTime.Today.AddDays(7)).Date,
            Status = StatusCobranca.Pendente,
            Forma = FormaPagamento.Pix
        };
        db.Cobrancas.Add(cobranca);
        await db.SaveChangesAsync();
        x.CobrancaId = cobranca.Id;
        await db.SaveChangesAsync();

        await RecarregarAsync();
        Mensagem = $"Cobrança de {x.Valor:C} gerada (vence em {cobranca.Vencimento:dd/MM/yyyy}). Veja em Cobranças e Extrato.";
    });

    [RelayCommand]
    private void ExcluirServico(Servico? s)
    {
        if (s is null) return;
        var id = s.Id;
        TextoConfirmacao = $"Excluir o serviço \"{s.Titulo}\"?" +
                           (s.CobrancaId is null ? "" : " A cobrança já gerada continua em Cobranças e Extrato.");
        _acaoPendente = async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            var x = await db.Servicos.FirstOrDefaultAsync(v => v.Id == id);
            if (x is not null) { db.Servicos.Remove(x); await db.SaveChangesAsync(); }
            if (ServicoEmEdicao?.Id == id) LimparForm();
            await RecarregarAsync();
            Mensagem = "Serviço excluído.";
        };
    }
}
