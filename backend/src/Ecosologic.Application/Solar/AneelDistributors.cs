using Ecosologic.Domain.Solar;

namespace Ecosologic.Application.Solar;

/// <summary>
/// Identidade canônica de uma distribuidora coberta pela sincronização ANEEL.
/// Distingue explicitamente três conceitos que a sincronização não pode confundir:
/// <see cref="ReportLabel"/> é o valor exibido/filtrado no relatório Power BI,
/// <see cref="CanonicalName"/> é o nome empresarial oficial armazenado e
/// <see cref="Distributor"/> é o valor de domínio persistido (enum). O cliente HTTP
/// consulta usando <see cref="ReportLabel"/>; o normalizador aceita tanto o rótulo do
/// relatório quanto o nome canônico e mapeia ambos para o mesmo <see cref="Distributor"/>.
/// </summary>
public sealed record DistributorIdentity(
    Distributor Distributor,
    string ReportLabel,
    string CanonicalName);

/// <summary>
/// Registro tipado das distribuidoras aceitas pela sincronização ANEEL. É a única
/// fonte de verdade para o mapeamento rótulo-do-relatório/nome-canônico/valor-de-domínio.
/// </summary>
public static class AneelDistributors
{
    public static readonly DistributorIdentity Light = new(
        Distributor.Light,
        ReportLabel: "Light",
        CanonicalName: "Light Serviços de Eletricidade S.A.");

    public static readonly DistributorIdentity EnelRio = new(
        Distributor.EnelRio,
        ReportLabel: "Enel RJ",
        CanonicalName: "Enel Distribuição Rio");

    public static readonly IReadOnlyList<DistributorIdentity> All = [Light, EnelRio];
}
