using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Data;
using GestorTI.Models;
using GestorTI.Services;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.ViewModels;

/// <summary>Orçamentos: itens, PDF, envio por WhatsApp e conversão em serviço quando aprovado.</summary>
public partial class OrcamentosViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly OrcamentoPdfService _pdf;
    private readonly SettingsService _settings;

    public OrcamentosViewModel(IDbContextFactory<AppDbContext> factory, OrcamentoPdfService pdf, SettingsService settings)
    {
        _factory = factory;
        _pdf = pdf;
        _settings = settings;
    }

    public ObservableCollection<Orcamento> Orcamentos { get; } = new();
    public ObservableCollection<Cliente> Clientes { get; } = new();
    public ObservableCollection<OrcamentoItem> Itens { get; } = new();

    // Formulário do orçamento
    [ObservableProperty] private Cliente? _clienteSelecionado;
    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _observacoes = "";
    [ObservableProperty] private DateTime? _validade = DateTime.Today.AddDays(15);

    // Formulário do item
    [ObservableProperty] private string _itemDescricao = "";
    [ObservableProperty] private decimal? _itemQuantidade = 1;
    [ObservableProperty] private decimal? _itemValor;

    [ObservableProperty] private string _totalTexto = "R$ 0,00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editando))]
    [NotifyPropertyChangedFor(nameof(TituloForm))]
    [NotifyPropertyChangedFor(nameof(TextoBotao))]
    private Orcamento? _orcamentoEmEdicao;

    public bool Editando => OrcamentoEmEdicao is not null;
    public string TituloForm => Editando ? $"Editar {OrcamentoEmEdicao!.NumeroTexto}" : "Novo orçamento";
    public string TextoBotao => Editando ? "Salvar alterações" : "Criar orçamento";

    // Confirmação
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
        var lista = await db.Orcamentos.AsNoTracking().Include(o => o.Cliente).Include(o => o.Itens).ToListAsync();
        var clientes = await db.Clientes.AsNoTracking().Where(c => c.Ativo).OrderBy(c => c.Nome).ToListAsync();

        Orcamentos.Clear();
        foreach (var o in lista.OrderByDescending(o => o.Numero)) Orcamentos.Add(o);
        Clientes.Clear();
        foreach (var c in clientes) Clientes.Add(c);
    }

    private void RecalcularTotal() =>
        TotalTexto = Itens.Sum(i => i.Total).ToString("C");

    private void LimparForm()
    {
        OrcamentoEmEdicao = null;
        ClienteSelecionado = null;
        Titulo = Observacoes = "";
        Validade = DateTime.Today.AddDays(15);
        Itens.Clear();
        LimparItem();
        RecalcularTotal();
    }

    private void LimparItem()
    {
        ItemDescricao = "";
        ItemQuantidade = 1;
        ItemValor = null;
    }

    // ---- itens ----

    [RelayCommand]
    private void AdicionarItem()
    {
        Mensagem = null;
        if (string.IsNullOrWhiteSpace(ItemDescricao)) { Mensagem = "Descreva o item."; return; }
        if (ItemQuantidade is null or <= 0) { Mensagem = "A quantidade deve ser maior que zero."; return; }
        if (ItemValor is null or < 0) { Mensagem = "Informe o valor unitário."; return; }

        Itens.Add(new OrcamentoItem
        {
            Descricao = ItemDescricao.Trim(),
            Quantidade = ItemQuantidade.Value,
            ValorUnitario = ItemValor.Value
        });
        LimparItem();
        RecalcularTotal();
    }

    [RelayCommand]
    private void RemoverItem(OrcamentoItem? item)
    {
        if (item is null) return;
        Itens.Remove(item);
        RecalcularTotal();
    }

    /// <summary>Devolve o item aos campos para ajustar e adicionar de novo.</summary>
    [RelayCommand]
    private void EditarItem(OrcamentoItem? item)
    {
        if (item is null) return;
        ItemDescricao = item.Descricao;
        ItemQuantidade = item.Quantidade;
        ItemValor = item.ValorUnitario;
        Itens.Remove(item);
        RecalcularTotal();
    }

    // ---- orçamento ----

    [RelayCommand]
    private Task SalvarAsync() => SafeAsync(async () =>
    {
        if (ClienteSelecionado is null) throw new InvalidOperationException("Selecione o cliente.");
        if (string.IsNullOrWhiteSpace(Titulo)) throw new InvalidOperationException("Informe o título do orçamento.");
        if (Itens.Count == 0) throw new InvalidOperationException("Adicione pelo menos um item.");
        if (Validade is null) throw new InvalidOperationException("Informe a validade.");

        await using var db = await _factory.CreateDbContextAsync();
        Orcamento o;
        if (OrcamentoEmEdicao is null)
        {
            var proximo = (await db.Orcamentos.MaxAsync(x => (int?)x.Numero) ?? 0) + 1;
            o = new Orcamento { Numero = proximo };
            db.Orcamentos.Add(o);
        }
        else
        {
            o = await db.Orcamentos.Include(x => x.Itens).FirstOrDefaultAsync(x => x.Id == OrcamentoEmEdicao.Id)
                ?? throw new InvalidOperationException("Orçamento não encontrado.");
            if (o.Status == StatusOrcamento.Aprovado)
                throw new InvalidOperationException("Orçamento aprovado não pode ser alterado.");
            db.OrcamentoItens.RemoveRange(o.Itens);
            o.Itens.Clear();
        }

        o.ClienteId = ClienteSelecionado.Id;
        o.Titulo = Titulo.Trim();
        o.Observacoes = string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim();
        o.Validade = Validade.Value.Date;
        foreach (var i in Itens)
            o.Itens.Add(new OrcamentoItem { Descricao = i.Descricao, Quantidade = i.Quantidade, ValorUnitario = i.ValorUnitario });

        await db.SaveChangesAsync();

        var criou = !Editando;
        var numero = o.NumeroTexto;
        LimparForm();
        await RecarregarAsync();
        Mensagem = criou ? $"Orçamento {numero} criado." : $"Orçamento {numero} atualizado.";
    });

    [RelayCommand]
    private void EditarOrcamento(Orcamento? o)
    {
        if (o is null) return;
        if (!o.PodeEditar) { Mensagem = "Orçamento aprovado não pode ser alterado."; return; }

        ClienteSelecionado = Clientes.FirstOrDefault(c => c.Id == o.ClienteId);
        Titulo = o.Titulo;
        Observacoes = o.Observacoes ?? "";
        Validade = o.Validade;
        Itens.Clear();
        foreach (var i in o.Itens)
            Itens.Add(new OrcamentoItem { Descricao = i.Descricao, Quantidade = i.Quantidade, ValorUnitario = i.ValorUnitario });
        LimparItem();
        RecalcularTotal();
        OrcamentoEmEdicao = o;
        Mensagem = null;
    }

    [RelayCommand]
    private void CancelarEdicao() => LimparForm();

    private async Task<Orcamento> CarregarCompletoAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Orcamentos.AsNoTracking().Include(x => x.Cliente).Include(x => x.Itens)
                   .FirstOrDefaultAsync(x => x.Id == id)
               ?? throw new InvalidOperationException("Orçamento não encontrado.");
    }

    private async Task MarcarEnviadoAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var o = await db.Orcamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (o is { Status: StatusOrcamento.Rascunho })
        {
            o.Status = StatusOrcamento.Enviado;
            await db.SaveChangesAsync();
        }
    }

    [RelayCommand]
    private Task GerarPdfAsync(Orcamento? orc) => SafeAsync(async () =>
    {
        if (orc is null) return;
        var o = await CarregarCompletoAsync(orc.Id);
        var arquivo = await Task.Run(() => _pdf.Gerar(o));
        Sistema.Abrir(arquivo);
        Mensagem = "PDF gerado e salvo na pasta do cliente: " + arquivo;
    });

    [RelayCommand]
    private Task EnviarWhatsAppAsync(Orcamento? orc) => SafeAsync(async () =>
    {
        if (orc is null) return;
        var o = await CarregarCompletoAsync(orc.Id);

        var numero = Sistema.NormalizarWhatsApp(o.Cliente.Telefone)
            ?? throw new InvalidOperationException(
                $"O cliente {o.Cliente.Nome} não tem um telefone/WhatsApp válido. Cadastre em Clientes (com DDD).");

        var arquivo = await Task.Run(() => _pdf.Gerar(o));
        var empresa = _settings.Current.NomeEmpresa;
        var mensagem =
            $"Olá, {o.Cliente.Nome.Split(' ')[0]}! Aqui é da {empresa}. " +
            $"Segue o orçamento {o.NumeroTexto} - {o.Titulo}, no valor de {o.Total:C}, " +
            $"válido até {o.Validade:dd/MM/yyyy}. O PDF está em anexo. Qualquer dúvida, é só chamar!";

        // O WhatsApp não permite anexar arquivo por link: abrimos a pasta com o PDF selecionado
        // e a conversa já com a mensagem pronta; o PDF é só arrastar (ou clipe > Documento).
        Sistema.MostrarArquivo(arquivo);
        Sistema.Abrir(Sistema.LinkWhatsApp(numero, mensagem));

        await MarcarEnviadoAsync(o.Id);
        await RecarregarAsync();
        Mensagem = "WhatsApp aberto com a mensagem pronta. Arraste o PDF da pasta para a conversa " +
                   "(ou use o clipe › Documento) e envie.";
    });

    [RelayCommand]
    private Task AprovarAsync(Orcamento? orc) => SafeAsync(async () =>
    {
        if (orc is null) return;
        await using var db = await _factory.CreateDbContextAsync();
        var o = await db.Orcamentos.Include(x => x.Itens).FirstOrDefaultAsync(x => x.Id == orc.Id)
                ?? throw new InvalidOperationException("Orçamento não encontrado.");
        if (!o.PodeAprovar) throw new InvalidOperationException("Este orçamento não pode mais ser aprovado.");

        var linhas = o.Itens.Select(i => $"• {i.Descricao} ({i.Quantidade:0.##} x {i.ValorUnitario:C})");
        var descricao = string.Join("\n", linhas);
        if (!string.IsNullOrWhiteSpace(o.Observacoes)) descricao += "\n\n" + o.Observacoes;

        var servico = new Servico
        {
            ClienteId = o.ClienteId,
            Titulo = o.Titulo,
            Categoria = "Outro",
            Descricao = descricao,
            Valor = o.Total,
            DataInicio = DateTime.Today,
            OrcamentoId = o.Id
        };
        db.Servicos.Add(servico);
        await db.SaveChangesAsync();

        o.Status = StatusOrcamento.Aprovado;
        o.ServicoId = servico.Id;
        await db.SaveChangesAsync();

        if (OrcamentoEmEdicao?.Id == o.Id) LimparForm();
        await RecarregarAsync();
        Mensagem = $"{o.NumeroTexto} aprovado! O serviço foi criado na aba Serviços.";
    });

    [RelayCommand]
    private Task RecusarAsync(Orcamento? orc) => SafeAsync(async () =>
    {
        if (orc is null) return;
        await using var db = await _factory.CreateDbContextAsync();
        var o = await db.Orcamentos.FirstOrDefaultAsync(x => x.Id == orc.Id)
                ?? throw new InvalidOperationException("Orçamento não encontrado.");
        if (!o.PodeAprovar) throw new InvalidOperationException("Este orçamento não pode ser recusado.");
        o.Status = StatusOrcamento.Recusado;
        await db.SaveChangesAsync();
        await RecarregarAsync();
        Mensagem = $"{o.NumeroTexto} marcado como recusado.";
    });

    [RelayCommand]
    private void ExcluirOrcamento(Orcamento? orc)
    {
        if (orc is null) return;
        var id = orc.Id;
        TextoConfirmacao = $"Excluir o orçamento {orc.NumeroTexto} - {orc.Titulo}? Esta ação não pode ser desfeita.";
        _acaoPendente = async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            var x = await db.Orcamentos.Include(o => o.Itens).FirstOrDefaultAsync(o => o.Id == id);
            if (x is not null) { db.Orcamentos.Remove(x); await db.SaveChangesAsync(); }
            if (OrcamentoEmEdicao?.Id == id) LimparForm();
            await RecarregarAsync();
            Mensagem = "Orçamento excluído.";
        };
    }
}
