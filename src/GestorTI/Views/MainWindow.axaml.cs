using Avalonia.Controls;
using GestorTI.ViewModels;

namespace GestorTI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm) await vm.IniciarAsync();
        };
    }
}
