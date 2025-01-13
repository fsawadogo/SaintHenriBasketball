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
}
