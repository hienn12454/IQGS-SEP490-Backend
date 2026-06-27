using DomainLayer.Exceptions;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Chuẩn hoá filter khoảng ngày cho các API thống kê/lịch sử.
/// Ưu tiên: fromDate/toDate tường minh &gt; year+month (1 tháng cụ thể) &gt; year (cả năm) &gt; không lọc.
/// </summary>
public static class DateRangeFilterHelper
{
    public static (DateTime? From, DateTime? To) Resolve(
        DateTime? fromDate, DateTime? toDate, int? year, int? month)
    {
        if (fromDate.HasValue || toDate.HasValue)
        {
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
                throw new BadRequestException("fromDate không được lớn hơn toDate.");

            return (
                fromDate.HasValue ? DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc) : null,
                toDate.HasValue ? DateTime.SpecifyKind(toDate.Value.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1) : null);
        }

        if (month.HasValue && !year.HasValue)
            throw new BadRequestException("Phải truyền year khi lọc theo month.");

        if (month.HasValue && (month < 1 || month > 12))
            throw new BadRequestException("month phải trong khoảng 1-12.");

        if (year.HasValue && month.HasValue)
        {
            var start = new DateTime(year.Value, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddMonths(1).AddTicks(-1));
        }

        if (year.HasValue)
        {
            var start = new DateTime(year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddYears(1).AddTicks(-1));
        }

        return (null, null);
    }
}
