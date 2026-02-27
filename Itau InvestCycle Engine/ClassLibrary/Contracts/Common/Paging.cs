namespace Itau.InvestCycleEngine.Contracts.Common;

public sealed record PagedRequest(int Page = 1, int PageSize = 20);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems);
