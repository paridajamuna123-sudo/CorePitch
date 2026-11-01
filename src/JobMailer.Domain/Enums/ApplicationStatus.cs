namespace JobMailer.Domain.Enums;

public enum ApplicationStatus
{
    Draft = 0,
    Queued = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4
}
