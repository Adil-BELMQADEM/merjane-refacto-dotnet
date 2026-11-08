using Moq;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;
using Refacto.DotNet.Controllers.Services.ProductHandlers;
using Refacto.DotNet.Controllers.Tests.Fakes;

namespace Refacto.DotNet.Controllers.Tests.Services.ProductHandlers
{
    public class SeasonalProductHandlerTests
    {
        private static readonly DateTime Now = new(2026, 7, 1, 10, 0, 0);
        private static readonly DateTime Today = Now.Date;

        private readonly Mock<INotificationService> _notificationService = new();
        private readonly SeasonalProductHandler _handler;

        public SeasonalProductHandlerTests()
        {
            _handler = new SeasonalProductHandler(_notificationService.Object, new FakeClock(Now));
        }

        [Fact]
        public void InSeasonAndInStock_DecrementsStock()
        {
            Product product = Seasonal(available: 30, leadTime: 15, start: Today.AddDays(-10), end: Today.AddDays(60));

            _handler.Handle(product);

            Assert.Equal(29, product.Available);
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void OutOfStockAndRestockedBeforeSeasonEnd_AnnouncesDelay()
        {
            Product product = Seasonal(available: 0, leadTime: 15, start: Today.AddDays(-10), end: Today.AddDays(60));

            _handler.Handle(product);

            Assert.Equal(0, product.Available);
            _notificationService.Verify(n => n.SendDelayNotification(15, product.Name!), Times.Once());
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void OutOfStockAndRestockedAfterSeasonEnd_IsUnavailable()
        {
            Product product = Seasonal(available: 0, leadTime: 90, start: Today.AddDays(-10), end: Today.AddDays(60));

            _handler.Handle(product);

            Assert.Equal(0, product.Available);
            _notificationService.Verify(n => n.SendOutOfStockNotification(product.Name!), Times.Once());
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonNotStarted_IsUnavailableButKeepsStock()
        {
            Product product = Seasonal(available: 30, leadTime: 15, start: Today.AddDays(1), end: Today.AddDays(60));

            _handler.Handle(product);

            Assert.Equal(30, product.Available);
            _notificationService.Verify(n => n.SendOutOfStockNotification(product.Name!), Times.Once());
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonOver_IsUnavailableAndStockCleared()
        {
            Product product = Seasonal(available: 30, leadTime: 15, start: Today.AddDays(-60), end: Today.AddDays(-1));

            _handler.Handle(product);

            Assert.Equal(0, product.Available);
            _notificationService.Verify(n => n.SendOutOfStockNotification(product.Name!), Times.Once());
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonStartsToday_IsNotSoldAndAnnouncesDelay_CurrentQuirk()
        {
            Product product = Seasonal(available: 30, leadTime: 15, start: Today, end: Today.AddDays(60));

            _handler.Handle(product);

            Assert.Equal(30, product.Available);
            _notificationService.Verify(n => n.SendDelayNotification(15, product.Name!), Times.Once());
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeasonEndsToday_IsUnavailableAndStockCleared_CurrentQuirk()
        {
            Product product = Seasonal(available: 30, leadTime: 15, start: Today.AddDays(-30), end: Today);

            _handler.Handle(product);

            Assert.Equal(0, product.Available);
            _notificationService.Verify(n => n.SendOutOfStockNotification(product.Name!), Times.Once());
            _notificationService.VerifyNoOtherCalls();
        }

        private static Product Seasonal(int available, int leadTime, DateTime start, DateTime end)
        {
            return new Product
            {
                Type = ProductType.Seasonal,
                Name = "Watermelon",
                Available = available,
                LeadTime = leadTime,
                SeasonStartDate = start,
                SeasonEndDate = end
            };
        }
    }
}
