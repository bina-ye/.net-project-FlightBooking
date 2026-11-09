using FlightBooking.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FlightBooking.Service.Payment;

public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;

    public MockPaymentService(ILogger<MockPaymentService> logger)
    {
        _logger = logger;
    }

    public Task<PaymentResult> ChargeAsync(
        decimal amount,
        string cardNumber,
        string expiry,
        string cvv,
        CancellationToken ct = default)
    {
        // סליקה מדומה: כרטיס שמסתיים ב-0000 נדחה, כל השאר מאושרים.
        // לא נרשמים ללוג מספר הכרטיס, התוקף או ה-CVV.
        if (cardNumber.EndsWith("0000"))
        {
            _logger.LogWarning(
                "תשלום נדחה בסכום {Amount}. סיבת הדחייה: כרטיס מדומה שנדחה.",
                amount);

            return Task.FromResult(
                new PaymentResult(false, null, "התשלום נדחה על ידי חברת האשראי."));
        }

        var transactionId = "MOCK-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        _logger.LogInformation(
            "תשלום אושר בסכום {Amount}. מזהה עסקה: {TransactionId}",
            amount,
            transactionId);

        return Task.FromResult(
            new PaymentResult(true, transactionId, null));
    }
}