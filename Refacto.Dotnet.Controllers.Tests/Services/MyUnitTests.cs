using Moq;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;
using Refacto.DotNet.Controllers.Services.ProductHandlers;

namespace Refacto.DotNet.Controllers.Tests.Services
{
    public class MyUnitTests
    {
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly NormalProductHandler _normalProductHandler;

        public MyUnitTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _normalProductHandler = new NormalProductHandler(_mockNotificationService.Object);
        }

        [Fact]
        public void Test()
        {
            // GIVEN
            Product product = new()
            {
                LeadTime = 15,
                Available = 0,
                Type = ProductType.Normal,
                Name = "RJ45 Cable"
            };

            // WHEN
            _normalProductHandler.Handle(product);

            // THEN
            Assert.Equal(0, product.Available);
            Assert.Equal(15, product.LeadTime);
            _mockNotificationService.Verify(service => service.SendDelayNotification(product.LeadTime, product.Name), Times.Once());
        }
    }
}
