using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Tests.Entities
{
    public class ProductTests
    {
        private static readonly DateTime Now = new(2026, 7, 1, 10, 0, 0);
        private static readonly DateTime Today = Now.Date;

        [Theory]
        [InlineData(-1, false)]
        [InlineData(0, false)]
        [InlineData(1, true)]
        public void IsInStock_WhenAvailableIsPositive(int available, bool expected)
        {
            Assert.Equal(expected, new Product { Available = available }.IsInStock());
        }

        [Theory]
        [InlineData(-1, false)]
        [InlineData(0, false)]
        [InlineData(1, true)]
        public void HasLeadTime_WhenLeadTimeIsPositive(int leadTime, bool expected)
        {
            Assert.Equal(expected, new Product { LeadTime = leadTime }.HasLeadTime());
        }

        [Theory]
        [InlineData(-1, 1, true)]
        [InlineData(0, 1, false)]
        [InlineData(-1, 0, false)]
        [InlineData(1, 10, false)]
        [InlineData(-10, -1, false)]
        [InlineData(null, 1, false)]
        [InlineData(-1, null, false)]
        public void IsInSeason_OnlyStrictlyBetweenSeasonBounds(int? startInDays, int? endInDays, bool expected)
        {
            Product product = new() { SeasonStartDate = DaysFromToday(startInDays), SeasonEndDate = DaysFromToday(endInDays) };

            Assert.Equal(expected, product.IsInSeason(Today));
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(-1, false)]
        [InlineData(null, false)]
        public void IsSeasonNotStartedAt_WhenSeasonStartsLater(int? startInDays, bool expected)
        {
            Product product = new() { SeasonStartDate = DaysFromToday(startInDays) };

            Assert.Equal(expected, product.IsSeasonNotStartedAt(Now));
        }

        [Theory]
        [InlineData(10, 5, true)]
        [InlineData(10, 10, true)]
        [InlineData(10, 11, false)]
        [InlineData(10, 20, false)]
        [InlineData(10, null, false)]
        public void IsRestockedAfterSeasonEnd_WhenLeadTimeGoesBeyondSeasonEnd(int leadTime, int? endInDays, bool expected)
        {
            Product product = new() { LeadTime = leadTime, SeasonEndDate = DaysFromToday(endInDays) };

            Assert.Equal(expected, product.IsRestockedAfterSeasonEnd(Now));
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(null, false)]
        public void IsNotExpiredOn_OnlyBeforeExpiryDate(int? expiryInDays, bool expected)
        {
            Product product = new() { ExpiryDate = DaysFromToday(expiryInDays) };

            Assert.Equal(expected, product.IsNotExpiredOn(Today));
        }

        [Fact]
        public void DecrementStock_RemovesOneUnit()
        {
            Product product = new() { Available = 5 };

            product.DecrementStock();

            Assert.Equal(4, product.Available);
        }

        [Fact]
        public void ClearStock_SetsStockToZero()
        {
            Product product = new() { Available = 5 };

            product.ClearStock();

            Assert.Equal(0, product.Available);
        }

        private static DateTime? DaysFromToday(int? days)
        {
            return days is null ? null : Today.AddDays(days.Value);
        }
    }
}
