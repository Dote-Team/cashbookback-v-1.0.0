using System;

namespace cashbook.Dto.audit;

public class AuditLogListItemDto
{
	public long Id { get; set; }

	public DateTime OccurredAt { get; set; }

	public Guid CorrelationId { get; set; }

	public string Category { get; set; } = string.Empty;

	public string CategoryLabel { get; set; } = string.Empty;

	public string Action { get; set; } = string.Empty;

	public string ActionLabel { get; set; } = string.Empty;

	public string Severity { get; set; } = string.Empty;

	public string SeverityLabel { get; set; } = string.Empty;

	public string? Summary { get; set; }

	public bool IsSuccess { get; set; }

	public int? StatusCode { get; set; }

	public Guid? UserId { get; set; }

	public string? Username { get; set; }

	public Guid? BusinessId { get; set; }

	public Guid? BookId { get; set; }

	public string? EntityName { get; set; }

	public string? EntityLabel { get; set; }

	public string? EntityId { get; set; }

	public string? Operation { get; set; }

	public string? OperationLabel { get; set; }

	public string? HttpMethod { get; set; }

	public string? Path { get; set; }

	public string? IpAddress { get; set; }

	public long? DurationMs { get; set; }

	public bool IsSlow { get; set; }
}
