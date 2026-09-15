namespace SaintHenriBasketball.Application.Helpers;

/// Keeps admin list paging in bounds so one request can't load a whole table.
public static class ListPaging
{
    public const int MaxPageSize = 500;

    public static (int Page, int PageSize) Clamp(int page, int pageSize, int defaultPageSize = 50) =>
        (Math.Max(1, page), pageSize < 1 ? defaultPageSize : Math.Min(pageSize, MaxPageSize));
}
