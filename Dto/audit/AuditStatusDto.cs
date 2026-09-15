using System;

namespace cashbook.Dto.audit;

public class AuditStatusDto
{
	public bool Enabled { get; set; }

	public int PendingInQueue { get; set; }

	public long DroppedTotal { get; set; }

	public long WrittenTotal { get; set; }

	public int RetentionDays { get; set; }

	public int SlowRequestMs { get; set; }

	public bool LogReadRequests { get; set; }

	public long TotalRows { get; set; }

	public DateTime? OldestRowUtc { get; set; }

	public DateTime? NewestRowUtc { get; set; }
}
