namespace Gateway.Domain;

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Declined = 2,
    Error = 3
}
