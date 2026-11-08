using Moq;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;
using Refacto.DotNet.Controllers.Services.ProductHandlers;

namespace Refacto.DotNet.Controllers.Tests.Services.ProductHandlers
{
    public class NormalProductHandlerTests
    {
        private readonly Mock<INotificationService> _notificationService = new();
        private readonly NormalProductHandler _handler;

        public NormalProductHandlerTests()
        {
            _handler = new NormalProductHandler(_notificationService.Object);
        }

        [Fact]
        public void InStock_DecrementsStock()
        {
            Product product = Normal(available: 3, leadTime: 15);

            _handler.Handle(product);

            Assert.Equal(2, product.Available);
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void OutOfStock_AnnouncesDelay()
        {
            Product product = Normal(available: 0, leadTime: 15);

            _handler.Handle(product);

            Assert.Equal(0, product.Available);
            _notificationService.Verify(n => n.SendDelayNotification(15, product.Name!), Times.Once());
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void OutOfStockWithoutLeadTime_DoesNothing()
        {
            Product product = Normal(available: 0, leadTime: 0);

            _handler.Handle(product);

            Assert.Equal(0, product.Available);
            _notificationService.VerifyNoOtherCalls();
        }

        private static Product Normal(int available, int leadTime)
        {
            return new Product { Type = ProductType.Normal, Name = "RJ45 Cable", Available = available, LeadTime = leadTime };
        }
    }
}
