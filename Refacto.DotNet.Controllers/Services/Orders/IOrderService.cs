namespace Refacto.DotNet.Controllers.Services.Orders
{
    public interface IOrderService
    {
        void ProcessOrder(long orderId);
    }
}
