namespace Ecosologic.Infrastructure.Persistence.Migrations;

/// <summary>
/// SQL que marca como incompletas as regras de compensação legadas introduzidas antes
/// das dimensões <c>Group</c>, <c>Subgroup</c> e <c>Modality</c> na tabela
/// <c>grid_compensation_rules</c>. O esquema antigo não possuía essas dimensões, então
/// as novas colunas são criadas como ANULÁVEIS e os registros legados são preservados
/// (nunca excluídos), porém marcados como incompletos para que não sejam elegíveis no
/// lookup até que sejam substituídos por regras completas.
/// </summary>
public static class GridRuleBackfillSql
{
    public const string MarkLegacyRulesIncomplete = """
        UPDATE grid_compensation_rules
        SET "IsComplete" = FALSE
        WHERE "Group" IS NULL OR "Subgroup" IS NULL OR "Modality" IS NULL;
        """;
}
