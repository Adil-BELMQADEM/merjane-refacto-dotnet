namespace Refacto.DotNet.Controllers.Services
{
    public class OrderNotFoundException : Exception
    {
        public OrderNotFoundException(long orderId)
            : base($"Order {orderId} not found")
        {
        }
    }
}
