using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Refacto.DotNet.Controllers.Common;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Dtos.Product;
using Refacto.DotNet.Controllers.Services.Impl;

namespace Refacto.DotNet.Controllers.Controllers
{
    [ApiController]
    [Route("orders")]
    public class OrdersController : ControllerBase
    {
        private readonly ProductService _ps;
        private readonly AppDbContext _ctx;
        private readonly IClock _clock;

        public OrdersController(ProductService ps, AppDbContext ctx, IClock clock)
        {
            _ps = ps;
            _ctx = ctx;
            _clock = clock;
        }

        [HttpPost("{orderId}/processOrder")]
        [ProducesResponseType(200)]
        public ActionResult<ProcessOrderResponse> ProcessOrder(long orderId)
        {
            Entities.Order? order = _ctx.Orders
                .Include(o => o.Items)
                .SingleOrDefault(o => o.Id == orderId);
            Console.WriteLine(order);
            List<long> ids = new() { orderId };
            ICollection<Entities.Product>? products = order.Items;

            foreach (Entities.Product p in products)
            {
                if (p.Type == Entities.ProductType.Normal)
                {
                    if (p.Available > 0)
                    {
                        p.Available -= 1;
                        _ctx.Entry(p).State = EntityState.Modified;
                        _ = _ctx.SaveChanges();

                    }
                    else
                    {
                        int leadTime = p.LeadTime;
                        if (leadTime > 0)
                        {
                            _ps.NotifyDelay(leadTime, p);
                        }
                    }
                }
                else if (p.Type == Entities.ProductType.Seasonal)
                {
                    if (_clock.Now.Date > p.SeasonStartDate && _clock.Now.Date < p.SeasonEndDate && p.Available > 0)
                    {
                        p.Available -= 1;
                        _ = _ctx.SaveChanges();
                    }
                    else
                    {
                        _ps.HandleSeasonalProduct(p);
                    }
                }
                else if (p.Type == Entities.ProductType.Expirable)
                {
                    if (p.Available > 0 && p.ExpiryDate > _clock.Now.Date)
                    {
                        p.Available -= 1;
                        _ = _ctx.SaveChanges();
                    }
                    else
                    {
                        _ps.HandleExpiredProduct(p);
                    }
                }
            }

            return new ProcessOrderResponse(order.Id);
        }
    }
}
