using SaintHenriBasketball.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaintHenriBasketball.Application.DTOs.Season;

public class UpdateSeasonDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
    /// Nullable on purpose: this DTO is applied field by field, so a non-nullable capacity would
    /// silently reset every season to 15 on any edit that did not send it.
    public int? SeasonPassCapacity { get; set; }
}
