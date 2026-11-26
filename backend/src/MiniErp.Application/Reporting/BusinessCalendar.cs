namespace MiniErp.Application.Reporting;

public static class BusinessCalendar
{
    // All ERP accounting periods use Vietnam standard time, including imported history.
    public const int OffsetHours = 7;

    public static DateOnly DateAt(DateTime utc) =>
        DateOnly.FromDateTime(utc.AddHours(OffsetHours));

    public static DateTime StartUtc(DateOnly day) =>
        day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-OffsetHours);
}
