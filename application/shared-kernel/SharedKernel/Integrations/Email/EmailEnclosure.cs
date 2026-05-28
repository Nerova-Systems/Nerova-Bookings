namespace SharedKernel.Integrations.Email;

/// <summary>
///     A binary file attached to an outbound <see cref="EmailMessage" />. Used for calendar
///     invites (.ics) on booking lifecycle emails and any other transport-agnostic enclosure.
/// </summary>
public sealed record EmailEnclosure(string FileName, string ContentType, byte[] ContentBytes);
