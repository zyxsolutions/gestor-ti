using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GestorTI.ViewModels;

namespace GestorTI.Views;

public partial class NotasFiscaisView : UserControl
{
    public NotasFiscaisView() => InitializeComponent();

    /// <summary>
    /// Abre o seletor de arquivos do Windows e entrega os caminhos à ViewModel, que copia
    /// os arquivos para a pasta do cliente. O seletor fica na View porque depende da janela.
    /// </summary>
    private async void OnImportarClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NotasFiscaisViewModel vm) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var arquivos = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Selecione as notas fiscais (PDF/XML)",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Notas fiscais (PDF e XML)") { Patterns = new[] { "*.pdf", "*.xml" } },
                    FilePickerFileTypes.All
                }
            });

            var caminhos = arquivos
                .Select(a => a.TryGetLocalPath())
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p!)
                .ToList();

            await vm.ImportarArquivosAsync(caminhos);
        }
        catch (Exception ex)
        {
            vm.Mensagem = "Erro ao importar: " + ex.Message;
        }
    }
}
