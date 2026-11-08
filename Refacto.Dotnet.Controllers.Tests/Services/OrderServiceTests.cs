using Moq;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Repositories;
using Refacto.DotNet.Controllers.Services;
using Refacto.DotNet.Controllers.Services.Impl;
using Refacto.DotNet.Controllers.Services.ProductHandlers;

namespace Refacto.DotNet.Controllers.Tests.Services
{
    public class OrderServiceTests
    {
        private const long OrderId = 1;

        private readonly Mock<IOrderRepository> _orderRepository = new();
        private readonly Mock<IProductHandler> _normalHandler = HandlerFor(ProductType.Normal);
        private readonly Mock<IProductHandler> _seasonalHandler = HandlerFor(ProductType.Seasonal);
        private readonly OrderService _orderService;

        public OrderServiceTests()
        {
            _orderService = new OrderService(_orderRepository.Object, new[] { _normalHandler.Object, _seasonalHandler.Object });
        }

        [Fact]
        public void ProcessOrder_DelegatesEachProductToTheHandlerOfItsType()
        {
            Product normal = new() { Type = ProductType.Normal };
            Product seasonal = new() { Type = ProductType.Seasonal };
            GivenOrder(normal, seasonal);

            _orderService.ProcessOrder(OrderId);

            _normalHandler.Verify(h => h.Handle(normal), Times.Once());
            _seasonalHandler.Verify(h => h.Handle(seasonal), Times.Once());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("UNKNOWN")]
        public void ProcessOrder_IgnoresProductsWithoutKnownType(string? type)
        {
            GivenOrder(new Product { Type = type });

            _orderService.ProcessOrder(OrderId);

            _normalHandler.Verify(h => h.Handle(It.IsAny<Product>()), Times.Never());
            _seasonalHandler.Verify(h => h.Handle(It.IsAny<Product>()), Times.Never());
        }

        [Fact]
        public void ProcessOrder_WithoutItems_OnlySaves()
        {
            _orderRepository.Setup(r => r.FindByIdWithItems(OrderId)).Returns(new Order { Id = OrderId, Items = null });

            _orderService.ProcessOrder(OrderId);

            _normalHandler.Verify(h => h.Handle(It.IsAny<Product>()), Times.Never());
            _orderRepository.Verify(r => r.SaveChanges(), Times.Once());
        }

        [Fact]
        public void ProcessOrder_SavesChangesOnce()
        {
            GivenOrder(new Product { Type = ProductType.Normal }, new Product { Type = ProductType.Seasonal });

            _orderService.ProcessOrder(OrderId);

            _orderRepository.Verify(r => r.SaveChanges(), Times.Once());
        }

        [Fact]
        public void ProcessOrder_UnknownOrder_Throws()
        {
            _orderRepository.Setup(r => r.FindByIdWithItems(OrderId)).Returns((Order?)null);

            Assert.Throws<OrderNotFoundException>(() => _orderService.ProcessOrder(OrderId));
            _orderRepository.Verify(r => r.SaveChanges(), Times.Never());
        }

        private void GivenOrder(params Product[] products)
        {
            _orderRepository
                .Setup(r => r.FindByIdWithItems(OrderId))
                .Returns(new Order { Id = OrderId, Items = products });
        }

        private static Mock<IProductHandler> HandlerFor(string productType)
        {
            Mock<IProductHandler> handler = new();
            handler.Setup(h => h.SupportedType).Returns(productType);
            return handler;
        }
    }
}
