using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Dtos.Product;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.Dotnet.Controllers.Tests.Controllers
{
    /// <summary>
    /// Characterization tests: they pin down the CURRENT behaviour of POST /orders/{id}/processOrder
    /// (including the odd cases) so the refactoring can be done without regression.
    /// </summary>
    [Collection("Sequential")]
    public class MyControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly AppDbContext _context;
        private readonly Mock<INotificationService> _mockNotificationService;

        public MyControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _mockNotificationService = new Mock<INotificationService>();

            _factory = factory.WithWebHostBuilder(builder =>
            {
                _ = builder.ConfigureServices(services =>
                {
                    _ = services.AddSingleton(_mockNotificationService.Object);

                    ServiceDescriptor? descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                    {
                        _ = services.Remove(descriptor);
                    }

                    // Add ApplicationDbContext using an in-memory database for testing
                    _ = services.AddDbContext<AppDbContext>(options =>
                    {
                        _ = options.UseInMemoryDatabase($"InMemoryDbForTesting-{GetType()}");
                    });
                    _ = services.AddScoped((_sp) => _mockNotificationService.Object);
                });
            });

            IServiceScope scope = _factory.Services.CreateScope();
            _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _ = _context.Database.EnsureDeleted();
            _ = _context.Database.EnsureCreated();
        }

        // ---------------------------------------------------------------- Order

        [Fact]
        public async Task ProcessOrderShouldReturn()
        {
            HttpClient client = _factory.CreateClient();

            List<Product> allProducts = CreateProducts();
            HashSet<Product> orderItems = new(allProducts);
            Order order = CreateOrder(orderItems);
            await _context.Products.AddRangeAsync(allProducts);
            _ = await _context.Orders.AddAsync(order);
            _ = await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            HttpResponseMessage response = await client.PostAsync($"/orders/{order.Id}/processOrder", null);
            _ = response.EnsureSuccessStatusCode();

            Order? resultOrder = await _context.Orders.FindAsync(order.Id);
            Assert.Equal(resultOrder!.Id, order.Id);
        }

        [Fact]
        public async Task ProcessOrder_ReturnsTheOrderId()
        {
            Order order = await SeedOrderAsync(Normal("USB Cable", available: 30, leadTime: 15));

            HttpResponseMessage response = await _factory.CreateClient().PostAsync($"/orders/{order.Id}/processOrder", null);

            _ = response.EnsureSuccessStatusCode();
            ProcessOrderResponse? body = await response.Content.ReadFromJsonAsync<ProcessOrderResponse>();
            Assert.Equal(order.Id, body!.id);
        }

        [Fact]
        public async Task ProcessOrder_WithProductsOfEveryType_AppliesTheRuleOfEachProduct()
        {
            List<Product> products = CreateProducts();
            Order order = await SeedOrderAsync(products.ToArray());

            await ProcessAsync(order);

            Assert.Equal(29, Reload(products[0]).Available); // NORMAL in stock
            Assert.Equal(0, Reload(products[1]).Available);  // NORMAL out of stock
            Assert.Equal(29, Reload(products[2]).Available); // EXPIRABLE not expired
            Assert.Equal(0, Reload(products[3]).Available);  // EXPIRABLE expired
            Assert.Equal(29, Reload(products[4]).Available); // SEASONAL in season
            Assert.Equal(30, Reload(products[5]).Available); // SEASONAL not started yet
            _mockNotificationService.Verify(n => n.SendDelayNotification(10, "USB Dongle"), Times.Once());
            _mockNotificationService.Verify(n => n.SendExpirationNotification("Milk", products[3].ExpiryDate!.Value), Times.Once());
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Grapes"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ProcessOrder_UnknownOrder_CurrentlyFailsWithServerError()
        {
            // Current behaviour: NullReferenceException -> 500. A 404 would be better (to discuss).
            HttpResponseMessage response = await _factory.CreateClient().PostAsync("/orders/999/processOrder", null);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task ProductWithUnknownType_IsLeftUntouched()
        {
            Product product = new() { Type = "UNKNOWN", Name = "Mystery", Available = 5, LeadTime = 3 };

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(5, Reload(product).Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ProcessOrder_WithoutItems_Succeeds()
        {
            Order order = await SeedOrderAsync();

            await ProcessAsync(order);

            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ProductType_IsCaseSensitive_LowercaseTypeIsIgnored_CurrentQuirk()
        {
            Product product = new() { Type = "normal", Name = "USB Cable", Available = 30, LeadTime = 15 };

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(30, Reload(product).Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        // --------------------------------------------------------------- NORMAL

        [Fact]
        public async Task Normal_InStock_DecrementsAvailable()
        {
            Product product = Normal("USB Cable", available: 30, leadTime: 15);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(29, Reload(product).Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Normal_OutOfStock_NotifiesDelay()
        {
            Product product = Normal("USB Dongle", available: 0, leadTime: 10);

            await ProcessAsync(await SeedOrderAsync(product));

            Product reloaded = Reload(product);
            Assert.Equal(0, reloaded.Available);
            Assert.Equal(10, reloaded.LeadTime);
            _mockNotificationService.Verify(n => n.SendDelayNotification(10, "USB Dongle"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Normal_OutOfStockWithoutLeadTime_DoesNothing()
        {
            Product product = Normal("HDMI Cable", available: 0, leadTime: 0);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(0, Reload(product).Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        // ------------------------------------------------------------- SEASONAL

        [Fact]
        public async Task Seasonal_InSeasonAndInStock_DecrementsAvailable()
        {
            Product product = Seasonal("Watermelon", available: 30, leadTime: 15, startInDays: -2, endInDays: 58);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(29, Reload(product).Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Seasonal_OutOfStockAndRestockedBeforeSeasonEnd_NotifiesDelay()
        {
            Product product = Seasonal("Watermelon", available: 0, leadTime: 15, startInDays: -2, endInDays: 58);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(0, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendDelayNotification(15, "Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Seasonal_OutOfStockAndRestockedAfterSeasonEnd_NotifiesOutOfStock()
        {
            Product product = Seasonal("Watermelon", available: 0, leadTime: 90, startInDays: -2, endInDays: 58);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(0, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Watermelon"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Seasonal_SeasonNotStarted_NotifiesOutOfStockAndKeepsStock()
        {
            Product product = Seasonal("Grapes", available: 30, leadTime: 15, startInDays: 180, endInDays: 240);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(30, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Grapes"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Seasonal_SeasonOver_NotifiesOutOfStockAndEmptiesStock()
        {
            Product product = Seasonal("Strawberry", available: 30, leadTime: 15, startInDays: -60, endInDays: -1);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(0, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Strawberry"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Seasonal_SeasonStartsTomorrow_NotifiesOutOfStockAndKeepsStock()
        {
            Product product = new()
            {
                Type = "SEASONAL", Name = "Apricot", Available = 30, LeadTime = 15,
                SeasonStartDate = DateTime.Now.Date.AddDays(1), SeasonEndDate = DateTime.Now.AddDays(58)
            };

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(30, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Apricot"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Seasonal_SeasonStartsToday_IsNotSoldAndNotifiesDelay_CurrentQuirk()
        {
            // Quirk: season bounds are exclusive, so on the first day the product is not sold even if in stock.
            Product product = new()
            {
                Type = "SEASONAL", Name = "Peach", Available = 30, LeadTime = 15,
                SeasonStartDate = DateTime.Now.Date, SeasonEndDate = DateTime.Now.AddDays(58)
            };

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(30, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendDelayNotification(15, "Peach"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Seasonal_SeasonEndsToday_EmptiesStockAndNotifiesOutOfStock_CurrentQuirk()
        {
            // Quirk: on the last day the product is not sold and its whole stock is set to 0.
            Product product = new()
            {
                Type = "SEASONAL", Name = "Cherry", Available = 30, LeadTime = 15,
                SeasonStartDate = DateTime.Now.AddDays(-30), SeasonEndDate = DateTime.Now.Date
            };

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(0, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendOutOfStockNotification("Cherry"), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        // ------------------------------------------------------------ EXPIRABLE

        [Fact]
        public async Task Expirable_NotExpiredAndInStock_DecrementsAvailable()
        {
            Product product = Expirable("Butter", available: 30, expiresInDays: 26);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(29, Reload(product).Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Expirable_Expired_NotifiesExpirationAndEmptiesStock()
        {
            Product product = Expirable("Milk", available: 6, expiresInDays: -2);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(0, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendExpirationNotification("Milk", product.ExpiryDate!.Value), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Expirable_ExpiringToday_IsConsideredExpired()
        {
            Product product = new() { Type = "EXPIRABLE", Name = "Yogurt", Available = 6, LeadTime = 5, ExpiryDate = DateTime.Now.Date };

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(0, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendExpirationNotification("Yogurt", product.ExpiryDate!.Value), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Expirable_ExpiringTomorrow_IsStillSold()
        {
            Product product = new() { Type = "EXPIRABLE", Name = "Cream", Available = 6, LeadTime = 5, ExpiryDate = DateTime.Now.Date.AddDays(1) };

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(5, Reload(product).Available);
            _mockNotificationService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Expirable_WithoutExpiryDate_CurrentlyFailsWithServerError_CurrentQuirk()
        {
            // Quirk: (DateTime)p.ExpiryDate throws on null -> 500, stock untouched.
            Product product = new() { Type = "EXPIRABLE", Name = "Honey", Available = 6, LeadTime = 5, ExpiryDate = null };
            Order order = await SeedOrderAsync(product);

            HttpResponseMessage response = await _factory.CreateClient().PostAsync($"/orders/{order.Id}/processOrder", null);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(6, Reload(product).Available);
        }

        [Fact]
        public async Task Expirable_NotExpiredButOutOfStock_NotifiesExpiration_CurrentQuirk()
        {
            // Quirk: an out-of-stock product that is NOT expired gets an expiration notification (no delay notification).
            Product product = Expirable("Cheese", available: 0, expiresInDays: 26);

            await ProcessAsync(await SeedOrderAsync(product));

            Assert.Equal(0, Reload(product).Available);
            _mockNotificationService.Verify(n => n.SendExpirationNotification("Cheese", product.ExpiryDate!.Value), Times.Once());
            _mockNotificationService.VerifyNoOtherCalls();
        }

        // -------------------------------------------------------------- Helpers

        private async Task<Order> SeedOrderAsync(params Product[] products)
        {
            Order order = CreateOrder(new HashSet<Product>(products));
            await _context.Products.AddRangeAsync(products);
            _ = await _context.Orders.AddAsync(order);
            _ = await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            return order;
        }

        private async Task ProcessAsync(Order order)
        {
            HttpResponseMessage response = await _factory.CreateClient().PostAsync($"/orders/{order.Id}/processOrder", null);
            _ = response.EnsureSuccessStatusCode();
        }

        private Product Reload(Product product)
        {
            _context.ChangeTracker.Clear();
            return _context.Products.AsNoTracking().Single(p => p.Id == product.Id);
        }

        private static Product Normal(string name, int available, int leadTime)
        {
            return new Product { Type = "NORMAL", Name = name, Available = available, LeadTime = leadTime };
        }

        private static Product Seasonal(string name, int available, int leadTime, int startInDays, int endInDays)
        {
            return new Product
            {
                Type = "SEASONAL", Name = name, Available = available, LeadTime = leadTime,
                SeasonStartDate = DateTime.Now.AddDays(startInDays), SeasonEndDate = DateTime.Now.AddDays(endInDays)
            };
        }

        private static Product Expirable(string name, int available, int expiresInDays)
        {
            return new Product
            {
                Type = "EXPIRABLE", Name = name, Available = available, LeadTime = 15,
                ExpiryDate = DateTime.Now.AddDays(expiresInDays)
            };
        }

        private static Order CreateOrder(HashSet<Product> products)
        {
            return new Order { Items = products };
        }

        private static List<Product> CreateProducts()
        {
            return new List<Product>
            {
                new Product { LeadTime = 15, Available = 30, Type = "NORMAL", Name = "USB Cable" },
                new Product { LeadTime = 10, Available = 0, Type = "NORMAL", Name = "USB Dongle" },
                new Product { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = DateTime.Now.AddDays(26) },
                new Product { LeadTime = 90, Available = 6, Type = "EXPIRABLE", Name = "Milk", ExpiryDate = DateTime.Now.AddDays(-2) },
                new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = DateTime.Now.AddDays(-2), SeasonEndDate = DateTime.Now.AddDays(58) },
                new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Grapes", SeasonStartDate = DateTime.Now.AddDays(180), SeasonEndDate = DateTime.Now.AddDays(240) }
            };
        }
    }
}
