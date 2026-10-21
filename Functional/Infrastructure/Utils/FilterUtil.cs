namespace Infrastructure.Utils;

public static class FilterUtil
{
    public static int PaginationOffset(int pageNumber, int pageSize)
    {
        return (pageNumber - 1) * pageSize;
    }

    public static int PaginationTotalPages(int totalCount, int pageSize)
    {
        if (pageSize == 0) return 0;
        return (int)Math.Ceiling((double)totalCount / pageSize);
    }

    public static string FilterLike(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "%";
        return $"%{value.Replace("%", "\\%").Replace("_", "\\_")}%";
    }
}
