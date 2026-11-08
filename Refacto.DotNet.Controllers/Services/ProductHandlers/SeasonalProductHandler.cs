using Refacto.DotNet.Controllers.Common;
using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services.ProductHandlers
{
    public sealed class SeasonalProductHandler : IProductHandler
    {
        private readonly INotificationService _notificationService;
        private readonly IClock _clock;

        public SeasonalProductHandler(INotificationService notificationService, IClock clock)
        {
            _notificationService = notificationService;
            _clock = clock;
        }

        public string SupportedType => ProductType.Seasonal;

        public void Handle(Product product)
        {
            if (product.IsInSeason(_clock.Today) && product.IsInStock())
            {
                product.DecrementStock();
            }
            else if (product.IsRestockedAfterSeasonEnd(_clock.Now))
            {
                _notificationService.SendOutOfStockNotification(product.Name!);
                product.ClearStock();
            }
            else if (product.IsSeasonNotStartedAt(_clock.Now))
            {
                _notificationService.SendOutOfStockNotification(product.Name!);
            }
            else
            {
                _notificationService.SendDelayNotification(product.LeadTime, product.Name!);
            }
        }
    }
}
