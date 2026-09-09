using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Ecosologic.Infrastructure.Solar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/tariffs")]
[Authorize(Roles = "Admin")]
public sealed class TariffsController(TariffCatalog catalog) : ControllerBase
{
    [HttpGet("profiles")]
    public async Task<IActionResult> List(
        string? distributor,
        string? group,
        string? subgroup,
        string? modality,
        string? post,
        DateOnly? date,
        bool? isComplete,
        CancellationToken cancellationToken)
    {
        var parseOk = TryParse<Distributor>(distributor, out var parsedDistributor, out var distributorError);
        parseOk &= TryParse<TariffGroup>(group, out var parsedGroup, out var groupError);
        parseOk &= TryParse<TariffSubgroup>(subgroup, out var parsedSubgroup, out var subgroupError);
        parseOk &= TryParse<TariffModality>(modality, out var parsedModality, out var modalityError);
        parseOk &= TryParse<TariffPost>(post, out var parsedPost, out var postError);

        if (!parseOk)
        {
            var errors = new Dictionary<string, string[]>();
            if (distributorError is not null) errors["distributor"] = [distributorError];
            if (groupError is not null) errors["group"] = [groupError];
            if (subgroupError is not null) errors["subgroup"] = [subgroupError];
            if (modalityError is not null) errors["modality"] = [modalityError];
            if (postError is not null) errors["post"] = [postError];
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var filter = new TariffProfileFilter(
            parsedDistributor,
            parsedGroup,
            parsedSubgroup,
            parsedModality,
            parsedPost,
            date,
            isComplete);

        var profiles = await catalog.ListAsync(filter, cancellationToken);
        return Ok(profiles.Select(ToResponse).ToList());
    }

    [HttpGet("profiles/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var profile = await catalog.GetByIdAsync(id, cancellationToken);
        return profile is null ? NotFound() : Ok(ToResponse(profile));
    }

    [HttpGet("grid-rules")]
    public async Task<IActionResult> ListGridRules(
        string? distributor,
        string? group,
        string? subgroup,
        string? modality,
        string? post,
        int? referenceYear,
        DateOnly? date,
        bool? isComplete,
        CancellationToken cancellationToken)
    {
        var parseOk = TryParse<Distributor>(distributor, out var parsedDistributor, out var distributorError);
        parseOk &= TryParse<TariffGroup>(group, out var parsedGroup, out var groupError);
        parseOk &= TryParse<TariffSubgroup>(subgroup, out var parsedSubgroup, out var subgroupError);
        parseOk &= TryParse<TariffModality>(modality, out var parsedModality, out var modalityError);
        parseOk &= TryParse<TariffPost>(post, out var parsedPost, out var postError);

        if (!parseOk)
        {
            var errors = new Dictionary<string, string[]>();
            if (distributorError is not null) errors["distributor"] = [distributorError];
            if (groupError is not null) errors["group"] = [groupError];
            if (subgroupError is not null) errors["subgroup"] = [subgroupError];
            if (modalityError is not null) errors["modality"] = [modalityError];
            if (postError is not null) errors["post"] = [postError];
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var filter = new GridCompensationRuleFilter(
            parsedDistributor,
            parsedGroup,
            parsedSubgroup,
            parsedModality,
            parsedPost,
            referenceYear,
            date,
            isComplete ?? true);

        var rules = await catalog.ListGridCompensationRulesAsync(filter, cancellationToken);
        return Ok(rules.Select(ToGridRuleResponse).ToList());
    }

    private static bool TryParse<TEnum>(string? value, out TEnum? parsed, out string? error)
        where TEnum : struct, Enum
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result))
        {
            parsed = result;
            return true;
        }

        error = $"Valor inválido. Aceito: {string.Join(", ", Enum.GetNames<TEnum>())}.";
        return false;
    }

    private static TariffProfileResponse ToResponse(TariffProfileRecord profile) =>
        new(
            profile.Id,
            profile.Distributor.ToString(),
            profile.Group.ToString(),
            profile.Subgroup.ToString(),
            profile.Modality.ToString(),
            profile.ValidityStart,
            profile.ValidityEnd,
            profile.ResolutionCode,
            profile.SourceUrl,
            profile.SourceDocumentHash,
            profile.AccessedAt,
            profile.IsComplete,
            profile.Components
                .OrderBy(component => component.Post)
                .ThenBy(component => component.Kind)
                .Select(component => new TariffComponentResponse(
                    component.Kind.ToString(),
                    component.Unit.ToString(),
                    component.Post.ToString(),
                    component.Value,
                    component.TaxIncluded,
                    component.SourcePage))
                .ToList());

    private static GridCompensationRuleResponse ToGridRuleResponse(GridCompensationRuleRecord rule) =>
        new(
            rule.Id,
            rule.Distributor.ToString(),
            rule.Group?.ToString(),
            rule.Subgroup?.ToString(),
            rule.Modality?.ToString(),
            rule.Post.ToString(),
            rule.ReferenceYear,
            rule.ValidityStart,
            rule.ValidityEnd,
            rule.ProgressivePercent,
            rule.BaseComponent.ToString(),
            rule.ResolutionCode,
            rule.SourceUrl,
            rule.SourceDocumentHash,
            rule.AccessedAt,
            rule.IsComplete);
}

public sealed record TariffProfileResponse(
    Guid Id,
    string Distributor,
    string Group,
    string Subgroup,
    string Modality,
    DateOnly ValidityStart,
    DateOnly? ValidityEnd,
    string ResolutionCode,
    string SourceUrl,
    string? SourceDocumentHash,
    DateTimeOffset AccessedAt,
    bool IsComplete,
    IReadOnlyList<TariffComponentResponse> Components);

public sealed record TariffComponentResponse(
    string Kind,
    string Unit,
    string Post,
    decimal Value,
    bool TaxIncluded,
    string? SourcePage);

public sealed record GridCompensationRuleResponse(
    Guid Id,
    string Distributor,
    string? Group,
    string? Subgroup,
    string? Modality,
    string Post,
    int ReferenceYear,
    DateOnly ValidityStart,
    DateOnly? ValidityEnd,
    decimal ProgressivePercent,
    string BaseComponent,
    string ResolutionCode,
    string SourceUrl,
    string? SourceDocumentHash,
    DateTimeOffset AccessedAt,
    bool IsComplete);
