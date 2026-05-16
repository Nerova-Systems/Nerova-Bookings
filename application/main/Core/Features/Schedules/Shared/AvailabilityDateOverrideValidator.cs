using FluentValidation;

namespace Main.Features.Schedules.Shared;

public static class AvailabilityDateOverrideValidator
{
    public static void AddAvailabilityDateOverrideRules<T>(IRuleBuilderInitial<T, AvailabilityDateOverrideRequest[]?> ruleBuilder)
    {
        ruleBuilder.Must(HaveUniqueDates).WithMessage("Availability date overrides must have unique dates.");
        ruleBuilder.Must(NotOverlap).WithMessage("Availability date override windows cannot overlap on the same date.");
    }

    private static bool HaveUniqueDates(AvailabilityDateOverrideRequest[]? dateOverrides)
    {
        return dateOverrides is null || dateOverrides.Select(dateOverride => dateOverride.Date).Distinct().Count() == dateOverrides.Length;
    }

    private static bool NotOverlap(AvailabilityDateOverrideRequest[]? dateOverrides)
    {
        if (dateOverrides is null)
        {
            return true;
        }

        return dateOverrides.All(dateOverride =>
            {
                var windows = dateOverride.Windows.OrderBy(window => window.StartMinute).ToArray();
                return windows.Zip(windows.Skip(1)).All(pair => pair.First.EndMinute <= pair.Second.StartMinute);
            }
        );
    }
}
