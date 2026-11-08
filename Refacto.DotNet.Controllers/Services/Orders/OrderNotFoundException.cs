namespace Refacto.DotNet.Controllers.Services.Orders
{
    public sealed class OrderNotFoundException : Exception
    {
        public OrderNotFoundException(long orderId)
            : base($"Order {orderId} not found")
        {
        }
    }
}
