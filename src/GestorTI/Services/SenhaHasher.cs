using System.Security.Cryptography;

namespace GestorTI.Services;

/// <summary>
/// Hash de senha com PBKDF2-SHA256 (nativo do .NET, sem pacotes externos), sal aleatório por senha
/// e comparação em tempo constante. Formato: <c>pbkdf2-sha256$iterações$sal$hash</c> — o nº de iterações
/// fica gravado, então dá para aumentá-lo no futuro sem invalidar senhas antigas.
/// </summary>
public static class SenhaHasher
{
    private const string Algoritmo = "pbkdf2-sha256";
    private const int Iteracoes = 600_000; // recomendação OWASP para PBKDF2-HMAC-SHA256
    private const int TamanhoSal = 16;
    private const int TamanhoHash = 32;

    public static string Gerar(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return $"{Algoritmo}${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string armazenado, string senha)
    {
        try
        {
            var p = armazenado.Split('$');
            if (p.Length != 4 || p[0] != Algoritmo || !int.TryParse(p[1], out var iteracoes) || iteracoes < 1)
                return false;

            var sal = Convert.FromBase64String(p[2]);
            var esperado = Convert.FromBase64String(p[3]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, sal, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException) { return false; }
    }
}
