using System;

namespace cashbook.Dto.audit;

public class AuditLogRequest
{
	public Guid? BusinessId { get; set; }

	public Guid? BookId { get; set; }

	public Guid? CorrelationId { get; set; }

	public int? Days { get; set; }

	public DateTime? From { get; set; }

	public DateTime? To { get; set; }

	public Guid? UserId { get; set; }

	public Guid? SessionId { get; set; }

	public string? Username { get; set; }

	public string? IpAddress { get; set; }

	public string? EntityName { get; set; }

	public string? EntityId { get; set; }

	public string? Category { get; set; }

	public string? Action { get; set; }

	public string? Severity { get; set; }

	public bool? IsSuccess { get; set; }

	public bool? OnlyFailures { get; set; }

	public bool? OnlySlow { get; set; }

	public bool? OnlySecurityEvents { get; set; }

	public string? Search { get; set; }

	public int Page { get; set; } = 1;

	public int Take { get; set; } = 25;

	public string? SortBy { get; set; }

	public string? SortDirection { get; set; }
}
