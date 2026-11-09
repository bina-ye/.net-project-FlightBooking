namespace FlightBooking.Core.Interfaces;

public record PaymentResult(bool IsSuccess, string? TransactionId, string? ErrorMessage);

public interface IPaymentService
{
    Task<PaymentResult> ChargeAsync(
        decimal amount,
        string cardNumber,
        string expiry,
        string cvv,
        CancellationToken ct = default);
}