using FluentValidation;

namespace Main.Features.Schedules.Shared;

public static class AvailabilityWindowValidator
{
    public const string OverlapMessage = "Availability windows cannot overlap on the same day.";

    public static void AddAvailabilityWindowRules<T>(IRuleBuilderInitial<T, AvailabilityWindowRequest[]> ruleBuilder)
    {
        ruleBuilder
            .NotNull()
            .WithMessage("Availability windows are required.")
            .Must(NotHaveOverlaps)
            .WithMessage(OverlapMessage);
    }

    public static bool NotHaveOverlaps(AvailabilityWindowRequest[] availabilityWindows)
    {
        foreach (var day in Enumerable.Range(0, 7))
        {
            var windowsForDay = availabilityWindows
                .Where(window => window.Days.Contains(day))
                .OrderBy(window => window.StartMinute)
                .ThenBy(window => window.EndMinute)
                .ToArray();

            for (var index = 1; index < windowsForDay.Length; index++)
            {
                if (windowsForDay[index].StartMinute < windowsForDay[index - 1].EndMinute)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
