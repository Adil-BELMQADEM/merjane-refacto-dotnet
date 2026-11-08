using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services.ProductHandlers
{
    public sealed class NormalProductHandler : IProductHandler
    {
        private readonly INotificationService _notificationService;

        public NormalProductHandler(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public string SupportedType => ProductType.Normal;

        public void Handle(Product product)
        {
            if (product.IsInStock())
            {
                product.DecrementStock();
            }
            else if (product.HasLeadTime())
            {
                _notificationService.SendDelayNotification(product.LeadTime, product.Name!);
            }
        }
    }
}
