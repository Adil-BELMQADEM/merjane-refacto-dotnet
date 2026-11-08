using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Repositories;
using Refacto.DotNet.Controllers.Services.ProductHandlers;

namespace Refacto.DotNet.Controllers.Services.Orders
{
    public sealed class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly Dictionary<string, IProductHandler> _handlersByType;

        public OrderService(IOrderRepository orderRepository, IEnumerable<IProductHandler> handlers)
        {
            _orderRepository = orderRepository;
            _handlersByType = handlers.ToDictionary(h => h.SupportedType);
        }

        public void ProcessOrder(long orderId)
        {
            Order order = _orderRepository.FindByIdWithItems(orderId)
                ?? throw new OrderNotFoundException(orderId);

            foreach (Product product in order.Items ?? Enumerable.Empty<Product>())
            {
                FindHandlerFor(product)?.Handle(product);
            }

            _orderRepository.SaveChanges();
        }

        private IProductHandler? FindHandlerFor(Product product)
        {
            return product.Type is null ? null : _handlersByType.GetValueOrDefault(product.Type);
        }
    }
}
