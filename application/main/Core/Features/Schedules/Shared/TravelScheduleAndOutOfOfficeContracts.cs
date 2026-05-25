using JetBrains.Annotations;
using Main.Features.Schedules.Domain;
using SharedKernel.Domain;

namespace Main.Features.Schedules.Shared;

[PublicAPI]
public sealed record TravelScheduleResponse(
    TravelScheduleId Id,
    UserId UserId,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone,
    ScheduleId? ScheduleId
)
{
    public static TravelScheduleResponse From(TravelSchedule travel)
    {
        return new TravelScheduleResponse(
            travel.Id,
            travel.UserId,
            travel.StartDate,
            travel.EndDate,
            travel.TimeZone,
            travel.ScheduleId
        );
    }
}

[PublicAPI]
public sealed record TravelSchedulesResponse(TravelScheduleResponse[] TravelSchedules);

[PublicAPI]
public sealed record OutOfOfficeResponse(
    OutOfOfficeId Id,
    UserId UserId,
    DateOnly StartDate,
    DateOnly EndDate,
    UserId? ToUserId,
    string? Reason,
    string? Notes
)
{
    public static OutOfOfficeResponse From(OutOfOffice ooo)
    {
        return new OutOfOfficeResponse(
            ooo.Id,
            ooo.UserId,
            ooo.StartDate,
            ooo.EndDate,
            ooo.ToUserId,
            ooo.Reason,
            ooo.Notes
        );
    }
}

[PublicAPI]
public sealed record OutOfOfficesResponse(OutOfOfficeResponse[] OutOfOffices);
