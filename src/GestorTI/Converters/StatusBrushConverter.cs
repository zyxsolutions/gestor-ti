using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GestorTI.Converters;

/// <summary>
/// Converte um texto de status em cor de "badge" (estilo Asaas: fundo suave + texto escuro).
/// ConverterParameter="fg" devolve a cor do texto; sem parâmetro, a cor de fundo.
/// </summary>
public sealed class StatusBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (bg, fg) = Paleta(value?.ToString());
        return new SolidColorBrush(Color.Parse(parameter as string == "fg" ? fg : bg));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static (string Bg, string Fg) Paleta(string? s) => s switch
    {
        "Paga" or "Autorizada" or "Ativo" or "Entrada" => ("#E3F6EA", "#1B7F43"),
        "Pendente" or "Processando" or "Aguardando envio" or "Suspenso" => ("#FFF4DB", "#946200"),
        "Vencida" or "Rejeitada" or "Saída" => ("#FDE7E7", "#B42318"),
        _ => ("#EAECF0", "#475467") // Cancelada, Encerrada, etc.
    };
}
