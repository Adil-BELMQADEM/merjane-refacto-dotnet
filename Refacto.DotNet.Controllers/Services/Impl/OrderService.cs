using Refacto.DotNet.Controllers.Common;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Repositories;

namespace Refacto.DotNet.Controllers.Services.Impl
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ProductService _productService;
        private readonly IClock _clock;

        public OrderService(IOrderRepository orderRepository, ProductService productService, IClock clock)
        {
            _orderRepository = orderRepository;
            _productService = productService;
            _clock = clock;
        }

        public void ProcessOrder(long orderId)
        {
            Order order = _orderRepository.FindByIdWithItems(orderId)
                ?? throw new OrderNotFoundException(orderId);

            foreach (Product product in order.Items ?? Enumerable.Empty<Product>())
            {
                ProcessProduct(product);
            }

            _orderRepository.SaveChanges();
        }

        private void ProcessProduct(Product p)
        {
            if (p.Type == ProductType.Normal)
            {
                if (p.Available > 0)
                {
                    p.Available -= 1;
                }
                else if (p.LeadTime > 0)
                {
                    _productService.NotifyDelay(p.LeadTime, p);
                }
            }
            else if (p.Type == ProductType.Seasonal)
            {
                if (_clock.Now.Date > p.SeasonStartDate && _clock.Now.Date < p.SeasonEndDate && p.Available > 0)
                {
                    p.Available -= 1;
                }
                else
                {
                    _productService.HandleSeasonalProduct(p);
                }
            }
            else if (p.Type == ProductType.Expirable)
            {
                if (p.Available > 0 && p.ExpiryDate > _clock.Now.Date)
                {
                    p.Available -= 1;
                }
                else
                {
                    _productService.HandleExpiredProduct(p);
                }
            }
        }
    }
}
