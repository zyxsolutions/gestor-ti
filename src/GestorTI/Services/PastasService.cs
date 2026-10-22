using GestorTI.Models;

namespace GestorTI.Services;

public sealed record ArquivoCliente(string Nome, string Caminho, DateTime Data, string Tamanho);

/// <summary>
/// Organiza os arquivos fiscais em uma pasta por cliente:
/// <c>&lt;PastaNotas&gt;\Clientes\&lt;Nome do cliente&gt; - &lt;CPF/CNPJ&gt;\</c>.
/// O CPF/CNPJ no nome da pasta evita colisão entre clientes com nomes iguais.
/// </summary>
public sealed class PastasService
{
    private readonly SettingsService _settings;

    public PastasService(SettingsService settings) => _settings = settings;

    public string CaminhoDoCliente(Cliente cliente) =>
        Path.Combine(_settings.Current.PastaNotas, "Clientes", $"{Sanitizar(cliente.Nome)} - {cliente.CpfCnpj}");

    public bool Existe(Cliente cliente) => Directory.Exists(CaminhoDoCliente(cliente));

    /// <summary>Cria a pasta se não existir e devolve o caminho.</summary>
    public string Garantir(Cliente cliente)
    {
        var pasta = CaminhoDoCliente(cliente);
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    /// <summary>Copia arquivos do computador para a pasta do cliente. Nunca sobrescreve: renomeia com " (2)", " (3)"…</summary>
    public IReadOnlyList<string> Importar(Cliente cliente, IEnumerable<string> origens)
    {
        var destino = Garantir(cliente);
        var copiados = new List<string>();

        foreach (var origem in origens)
        {
            if (!File.Exists(origem)) continue;

            var nome = Path.GetFileNameWithoutExtension(origem);
            var ext = Path.GetExtension(origem);
            var alvo = Path.Combine(destino, nome + ext);
            for (var i = 2; File.Exists(alvo); i++)
                alvo = Path.Combine(destino, $"{nome} ({i}){ext}");

            File.Copy(origem, alvo);
            copiados.Add(Path.GetFileName(alvo));
        }
        return copiados;
    }

    public IReadOnlyList<ArquivoCliente> Listar(Cliente cliente)
    {
        var pasta = CaminhoDoCliente(cliente);
        if (!Directory.Exists(pasta)) return Array.Empty<ArquivoCliente>();

        return new DirectoryInfo(pasta)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new ArquivoCliente(f.Name, f.FullName, f.LastWriteTime, FormatarTamanho(f.Length)))
            .ToList();
    }

    public static string Sanitizar(string nome)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpo = new string(nome.Select(c => invalidos.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (limpo.Length == 0) limpo = "cliente";
        return limpo.Length > 50 ? limpo[..50].TrimEnd() : limpo;
    }

    private static string FormatarTamanho(long bytes) =>
        bytes < 1024 * 1024 ? $"{Math.Max(1, bytes / 1024)} KB" : $"{bytes / 1024d / 1024d:N1} MB";
}
