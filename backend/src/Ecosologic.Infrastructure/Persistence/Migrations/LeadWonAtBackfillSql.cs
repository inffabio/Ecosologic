namespace Ecosologic.Infrastructure.Persistence.Migrations;

/// <summary>
/// Backfill que aproxima <c>WonAt</c> para leads que já estavam em Stage "Won" antes da
/// introdução da coluna. Como o timestamp exato da vitória não era registrado, adotamos a
/// aproximação documentada: <c>WonAt = UpdatedAt</c>, caindo para <c>CreatedAt</c> quando
/// <c>UpdatedAt</c> é nulo. Leads em outras etapas não são alterados.
/// </summary>
public static class LeadWonAtBackfillSql
{
    public const string SetWonAtForExistingWonLeads = """
        UPDATE leads
        SET "WonAt" = COALESCE("UpdatedAt", "CreatedAt")
        WHERE "Stage" = 'Won';
        """;
}
