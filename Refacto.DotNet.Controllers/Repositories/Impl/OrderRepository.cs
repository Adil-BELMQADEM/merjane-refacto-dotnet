using Microsoft.EntityFrameworkCore;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Repositories.Impl
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;

        public OrderRepository(AppDbContext context)
        {
            _context = context;
        }

        public Order? FindByIdWithItems(long orderId)
        {
            return _context.Orders
                .Include(o => o.Items)
                .SingleOrDefault(o => o.Id == orderId);
        }

        public void SaveChanges()
        {
            _ = _context.SaveChanges();
        }
    }
}
