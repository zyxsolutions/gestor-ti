using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GestorTI.Services;

namespace GestorTI.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _svc;

    public DashboardViewModel(DashboardService svc) => _svc = svc;

    [ObservableProperty] private decimal _saldoCaixa;
    [ObservableProperty] private decimal _mrr;
    [ObservableProperty] private decimal _inadimplenciaValor;
    [ObservableProperty] private decimal _inadimplenciaPercentual;
    [ObservableProperty] private decimal _aReceberMes;
    [ObservableProperty] private decimal _recebidoMes;
    [ObservableProperty] private bool _semAlertas = true;

    public ObservableCollection<BarraMes> Barras { get; } = new();
    public ObservableCollection<ContratoAlerta> ContratosVencendo { get; } = new();

    public override Task CarregarAsync() => SafeAsync(async () =>
    {
        var d = await _svc.ObterAsync();
        SaldoCaixa = d.SaldoCaixa;
        Mrr = d.Mrr;
        InadimplenciaValor = d.InadimplenciaValor;
        InadimplenciaPercentual = d.InadimplenciaPercentual;
        AReceberMes = d.AReceberMes;
        RecebidoMes = d.RecebidoMes;

        Barras.Clear();
        foreach (var b in d.Barras) Barras.Add(b);

        ContratosVencendo.Clear();
        foreach (var a in d.ContratosVencendo) ContratosVencendo.Add(a);
        SemAlertas = ContratosVencendo.Count == 0;
    });
}
