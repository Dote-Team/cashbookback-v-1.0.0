using System;
using System.Collections.Generic;

namespace cashbook.Dto.audit;

public class AuditStatsDto
{
	public DateTime From { get; set; }

	public DateTime To { get; set; }

	public int RangeDays { get; set; }

	public int Total { get; set; }

	public int Failures { get; set; }

	public int SecurityEvents { get; set; }

	public int CriticalEvents { get; set; }

	public int SlowRequests { get; set; }

	public int DistinctUsers { get; set; }

	public double AverageDurationMs { get; set; }

	public List<AuditBreakdownItemDto> ByCategory { get; set; } = new List<AuditBreakdownItemDto>();

	public List<AuditBreakdownItemDto> BySeverity { get; set; } = new List<AuditBreakdownItemDto>();

	public List<AuditBreakdownItemDto> ByAction { get; set; } = new List<AuditBreakdownItemDto>();

	public List<AuditBreakdownItemDto> ByUser { get; set; } = new List<AuditBreakdownItemDto>();

	public List<AuditDailyPointDto> Daily { get; set; } = new List<AuditDailyPointDto>();
}
