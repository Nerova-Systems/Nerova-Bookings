using System.Globalization;

namespace Main.Tests;

internal static class DateDriftTestDates
{
    private static readonly DateOnly AnchorMonday = GetAnchorMonday();
    private static readonly DateOnly PastDate = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime).AddDays(-30);

    internal static string FutureDate(int daysFromAnchor)
    {
        return AnchorMonday.AddDays(daysFromAnchor).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    internal static string FutureDateTimeText(int daysFromAnchor, int hour, int minute, int second = 0)
    {
        return $"{FutureDate(daysFromAnchor)}T{hour:00}:{minute:00}:{second:00}Z";
    }

    internal static DateTimeOffset FutureDateTime(int daysFromAnchor, int hour, int minute, int second = 0)
    {
        return new DateTimeOffset(AnchorMonday.AddDays(daysFromAnchor).ToDateTime(new TimeOnly(hour, minute, second)), TimeSpan.Zero);
    }

    internal static string PastDateTimeText(int hour, int minute, int second = 0)
    {
        return $"{PastDate:yyyy-MM-dd}T{hour:00}:{minute:00}:{second:00}Z";
    }

    internal static DateTimeOffset PastDateTime(int hour, int minute, int second = 0)
    {
        return new DateTimeOffset(PastDate.ToDateTime(new TimeOnly(hour, minute, second)), TimeSpan.Zero);
    }

    private static DateOnly GetAnchorMonday()
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday).AddDays(7);
    }
}
