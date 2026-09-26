namespace Directory.Search;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record SearchPagedResult(
    IReadOnlyList<SearchResult> Items,
    int TotalCount,
    int Page,
    int PageSize);
