using System;

namespace cashbook.Dto.audit;

public class AuditDailyPointDto
{
	public DateTime Date { get; set; }

	public int Total { get; set; }

	public int Failures { get; set; }
}
