using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorTI.Data;
using GestorTI.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorTI.ViewModels;

public partial class ClientesViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ClientesViewModel(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public ObservableCollection<Cliente> Clientes { get; } = new();

    // Formulário
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private string _cpfCnpj = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _telefone = "";
    [ObservableProperty] private string _cep = "";
    [ObservableProperty] private string _logradouro = "";
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private string _bairro = "";
    [ObservableProperty] private string _codigoMunicipioIbge = "";
    [ObservableProperty] private string _uf = "";

    public override Task CarregarAsync() => SafeAsync(RecarregarAsync);

    private async Task RecarregarAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var lista = await db.Clientes.AsNoTracking().OrderBy(c => c.Nome).ToListAsync();
        Clientes.Clear();
        foreach (var c in lista) Clientes.Add(c);
    }

    [RelayCommand]
    private Task SalvarAsync() => SafeAsync(async () =>
    {
        var doc = Util.SomenteDigitos(CpfCnpj);
        if (string.IsNullOrWhiteSpace(Nome)) throw new InvalidOperationException("Informe o nome do cliente.");
        if (doc.Length != 11 && doc.Length != 14) throw new InvalidOperationException("CPF deve ter 11 dígitos e CNPJ 14.");

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Clientes.AnyAsync(c => c.CpfCnpj == doc))
            throw new InvalidOperationException("Já existe um cliente com este CPF/CNPJ.");

        db.Clientes.Add(new Cliente
        {
            Nome = Nome.Trim(),
            CpfCnpj = doc,
            Email = Email.Trim(),
            Telefone = Telefone.Trim(),
            Cep = Util.SomenteDigitos(Cep),
            Logradouro = Logradouro.Trim(),
            Numero = Numero.Trim(),
            Bairro = Bairro.Trim(),
            CodigoMunicipioIbge = Util.SomenteDigitos(CodigoMunicipioIbge),
            Uf = Uf.Trim().ToUpperInvariant()
        });
        await db.SaveChangesAsync();

        Nome = CpfCnpj = Email = Telefone = Cep = Logradouro = Numero = Bairro = CodigoMunicipioIbge = Uf = "";
        await RecarregarAsync();
        Mensagem = "Cliente cadastrado.";
    });

    [RelayCommand]
    private Task RemoverAsync(Cliente? cliente) => SafeAsync(async () =>
    {
        if (cliente is null) return;
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Contratos.AnyAsync(c => c.ClienteId == cliente.Id) ||
            await db.Cobrancas.AnyAsync(c => c.ClienteId == cliente.Id))
            throw new InvalidOperationException("Este cliente possui contratos ou cobranças e não pode ser excluído.");

        db.Clientes.Remove(new Cliente { Id = cliente.Id });
        await db.SaveChangesAsync();
        await RecarregarAsync();
        Mensagem = "Cliente excluído.";
    });
}
