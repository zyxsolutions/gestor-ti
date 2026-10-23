using Microsoft.EntityFrameworkCore;

namespace GestorTI.Data;

/// <summary>
/// Atualização de esquema simples e segura para bancos que já existem.
/// <c>EnsureCreated()</c> cria o banco inteiro na 1ª execução, mas NÃO adiciona tabelas a um banco existente.
/// Cada tabela nova entra aqui com <c>CREATE TABLE IF NOT EXISTS</c> (idempotente, não mexe em dados).
/// Quando o app crescer, trocar por migrations do EF Core.
/// </summary>
public static class SchemaUpdater
{
    public static void Atualizar(AppDbContext db)
    {
        // v1.1 — usuários e login
        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS \"Usuarios\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Usuarios\" PRIMARY KEY AUTOINCREMENT, " +
            "\"Nome\" TEXT NOT NULL, " +
            "\"Login\" TEXT NOT NULL, " +
            "\"SenhaHash\" TEXT NOT NULL, " +
            "\"Perfil\" TEXT NOT NULL, " +
            "\"Ativo\" INTEGER NOT NULL, " +
            "\"CriadoEm\" TEXT NOT NULL, " +
            "\"UltimoAcesso\" TEXT NULL);");
        db.Database.ExecuteSqlRaw(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Usuarios_Login\" ON \"Usuarios\" (\"Login\");");
    }
}
