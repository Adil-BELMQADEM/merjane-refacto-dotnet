using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Refacto.DotNet.Controllers.Entities
{
    [Table("products")]
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Column("lead_time")]
        public int LeadTime { get; set; }

        [Column("available")]
        public int Available { get; set; }

        [Column("type")]
        public string? Type { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("expiry_date")]
        public DateTime? ExpiryDate { get; set; }

        [Column("season_start_date")]
        public DateTime? SeasonStartDate { get; set; }

        [Column("season_end_date")]
        public DateTime? SeasonEndDate { get; set; }

        public bool IsInStock() => Available > 0;

        public bool HasLeadTime() => LeadTime > 0;

        public bool IsInSeason(DateTime today) => today > SeasonStartDate && today < SeasonEndDate;

        public bool IsSeasonNotStartedAt(DateTime now) => SeasonStartDate > now;

        public bool IsRestockedAfterSeasonEnd(DateTime now) => now.AddDays(LeadTime) > SeasonEndDate;

        public bool IsNotExpiredOn(DateTime today) => ExpiryDate > today;

        public void DecrementStock() => Available--;

        public void ClearStock() => Available = 0;
    }
}
