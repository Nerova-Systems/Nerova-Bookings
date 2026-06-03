using JetBrains.Annotations;

namespace Main.Features.Clients.Domain;

[PublicAPI]
public enum SortableClientProperties
{
    FirstVisitAt,
    LastVisitAt,
    Name,
    Email
}
