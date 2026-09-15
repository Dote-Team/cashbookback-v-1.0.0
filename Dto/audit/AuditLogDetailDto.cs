using System;

namespace cashbook.Dto.audit;

public class AuditLogDetailDto : AuditLogListItemDto
{
	public string? QueryString { get; set; }

	public string? DataJson { get; set; }

	public string? Error { get; set; }

	public string? UserAgent { get; set; }

	public Guid? SessionId { get; set; }

	public string? DeviceToken { get; set; }
}
