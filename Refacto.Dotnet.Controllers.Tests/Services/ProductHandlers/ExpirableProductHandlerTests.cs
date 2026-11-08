using Moq;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;
using Refacto.DotNet.Controllers.Services.ProductHandlers;
using Refacto.DotNet.Controllers.Tests.Fakes;

namespace Refacto.DotNet.Controllers.Tests.Services.ProductHandlers
{
    public class ExpirableProductHandlerTests
    {
        private static readonly DateTime Now = new(2026, 7, 1, 10, 0, 0);
        private static readonly DateTime Today = Now.Date;

        private readonly Mock<INotificationService> _notificationService = new();
        private readonly ExpirableProductHandler _handler;

        public ExpirableProductHandlerTests()
        {
            _handler = new ExpirableProductHandler(_notificationService.Object, new FakeClock(Now));
        }

        [Fact]
        public void NotExpiredAndInStock_DecrementsStock()
        {
            Product product = Expirable(available: 6, expiryDate: Today.AddDays(1));

            _handler.Handle(product);

            Assert.Equal(5, product.Available);
            _notificationService.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void ExpiredOrExpiringToday_IsUnavailableAndStockCleared(int expiryInDays)
        {
            Product product = Expirable(available: 6, expiryDate: Today.AddDays(expiryInDays));

            _handler.Handle(product);

            Assert.Equal(0, product.Available);
            _notificationService.Verify(n => n.SendExpirationNotification(product.Name!, product.ExpiryDate!.Value), Times.Once());
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void NotExpiredButOutOfStock_NotifiesExpiration_CurrentQuirk()
        {
            Product product = Expirable(available: 0, expiryDate: Today.AddDays(10));

            _handler.Handle(product);

            Assert.Equal(0, product.Available);
            _notificationService.Verify(n => n.SendExpirationNotification(product.Name!, product.ExpiryDate!.Value), Times.Once());
            _notificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void WithoutExpiryDate_Throws_CurrentQuirk()
        {
            Product product = new() { Type = ProductType.Expirable, Name = "Honey", Available = 6, ExpiryDate = null };

            Assert.Throws<InvalidOperationException>(() => _handler.Handle(product));
            Assert.Equal(6, product.Available);
        }

        private static Product Expirable(int available, DateTime expiryDate)
        {
            return new Product { Type = ProductType.Expirable, Name = "Butter", Available = available, ExpiryDate = expiryDate };
        }
    }
}
