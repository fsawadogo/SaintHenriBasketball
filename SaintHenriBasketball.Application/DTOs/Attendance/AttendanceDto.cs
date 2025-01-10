using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaintHenriBasketball.Application.DTOs.Attendance;
public class AttendanceDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string UserEmail { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string? Notes { get; set; }
}