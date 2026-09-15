using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class AuditLog
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public long Id { get; set; }

	public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

	public Guid CorrelationId { get; set; }

	[Required]
	[MaxLength(20)]
	public string Category { get; set; } = "Data";

	[Required]
	[MaxLength(80)]
	public string Action { get; set; } = string.Empty;

	[Required]
	[MaxLength(20)]
	public string Severity { get; set; } = "Info";

	[MaxLength(1000)]
	public string? Summary { get; set; }

	public bool IsSuccess { get; set; }

	public int? StatusCode { get; set; }

	public Guid? UserId { get; set; }

	[MaxLength(100)]
	public string? Username { get; set; }

	public Guid? BusinessId { get; set; }

	public Guid? SessionId { get; set; }

	[MaxLength(200)]
	public string? DeviceToken { get; set; }

	[MaxLength(100)]
	public string? EntityName { get; set; }

	[MaxLength(100)]
	public string? EntityId { get; set; }

	public Guid? BookId { get; set; }

	[MaxLength(20)]
	public string? Operation { get; set; }

	[MaxLength(10)]
	public string? HttpMethod { get; set; }

	[MaxLength(500)]
	public string? Path { get; set; }

	[MaxLength(2000)]
	public string? QueryString { get; set; }

	[MaxLength(64)]
	public string? IpAddress { get; set; }

	[MaxLength(500)]
	public string? UserAgent { get; set; }

	public long? DurationMs { get; set; }

	public bool IsSlow { get; set; }

	public string? DataJson { get; set; }

	[MaxLength(2000)]
	public string? Error { get; set; }
}
