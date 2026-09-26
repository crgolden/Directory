namespace Directory.Church;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
