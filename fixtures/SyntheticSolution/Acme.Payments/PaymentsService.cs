using Acme.Shared.Contracts;
using Grpc.Core;

namespace Acme.Payments;

public sealed class PaymentsService : Payments.PaymentsBase
{
    private readonly IEventBus _eventBus;

    public PaymentsService(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<OrderPlaced>(HandleOrderPlacedAsync);
    }

    public override async Task<AuthorizePaymentReply> AuthorizePayment(
        AuthorizePaymentRequest request, ServerCallContext context)
    {
        var paymentId = Guid.NewGuid();

        await _eventBus.PublishAsync(
            new PaymentProcessed(Guid.Parse(request.OrderId), paymentId, DateTimeOffset.UtcNow),
            context.CancellationToken);

        return new AuthorizePaymentReply { PaymentId = paymentId.ToString(), Approved = true };
    }

    public override async Task StreamPaymentStatus(
        StreamPaymentStatusRequest request,
        IServerStreamWriter<PaymentStatusUpdate> responseStream,
        ServerCallContext context)
    {
        string[] statuses = ["pending", "authorized", "settled"];
        foreach (var status in statuses)
        {
            await responseStream.WriteAsync(
                new PaymentStatusUpdate { PaymentId = request.PaymentId, Status = status });
        }
    }

    private Task HandleOrderPlacedAsync(OrderPlaced orderPlaced, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
