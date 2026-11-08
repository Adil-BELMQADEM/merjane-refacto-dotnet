using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Repositories
{
    public interface IOrderRepository
    {
        Order? FindByIdWithItems(long orderId);

        void SaveChanges();
    }
}
