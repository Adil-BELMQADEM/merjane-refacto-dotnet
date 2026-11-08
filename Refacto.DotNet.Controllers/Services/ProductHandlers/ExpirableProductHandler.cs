using Refacto.DotNet.Controllers.Common;
using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services.ProductHandlers
{
    public sealed class ExpirableProductHandler : IProductHandler
    {
        private readonly INotificationService _notificationService;
        private readonly IClock _clock;

        public ExpirableProductHandler(INotificationService notificationService, IClock clock)
        {
            _notificationService = notificationService;
            _clock = clock;
        }

        public string SupportedType => ProductType.Expirable;

        public void Handle(Product product)
        {
            if (product.IsInStock() && product.IsNotExpiredOn(_clock.Today))
            {
                product.DecrementStock();
            }
            else
            {
                _notificationService.SendExpirationNotification(product.Name!, product.ExpiryDate!.Value);
                product.ClearStock();
            }
        }
    }
}
