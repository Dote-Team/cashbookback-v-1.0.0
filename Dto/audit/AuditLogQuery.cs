using System;
using System.Collections.Generic;

namespace cashbook.Dto.audit;

public class AuditLogQuery
{
	public DateTime? From { get; set; }

	public DateTime? To { get; set; }

	public Guid? UserId { get; set; }

	public string? Username { get; set; }

	public Guid? SessionId { get; set; }

	public string? IpAddress { get; set; }

	public Guid? BusinessId { get; set; }

	public Guid? BookId { get; set; }

	public Guid? CorrelationId { get; set; }

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

	public int Skip { get; set; }

	public int Take { get; set; } = 25;

	public string SortBy { get; set; } = "date";

	public string SortDirection { get; set; } = "desc";

	public bool IsUnrestricted { get; set; }

	public Guid? ScopeBusinessId { get; set; }

	public List<Guid>? ScopeBookIds { get; set; }
}
