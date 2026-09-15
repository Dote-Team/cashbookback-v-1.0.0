using System;

namespace cashbook.Models;

public class AuditSettings
{
	public const string SectionName = "AuditSettings";

	public bool Enabled { get; set; } = true;

	public bool LogRequestBodies { get; set; } = true;

	public bool LogQueryStrings { get; set; } = true;

	public int MaxBodyLength { get; set; } = 8000;

	public int QueueCapacity { get; set; } = 5000;

	public bool LogReadRequests { get; set; } = true;

	public int BatchSize { get; set; } = 200;

	public int MaxChangesPerSave { get; set; } = 200;

	public int SlowRequestMs { get; set; } = 2000;

	public int RetentionDays { get; set; } = 365;

	public int RetentionSweepHours { get; set; } = 24;

	public int RetryDelaySeconds { get; set; } = 5;

	public string[] ExcludedPaths { get; set; } = Array.Empty<string>();

	public bool IncludeStackTrace { get; set; }

	public bool RetentionEnabled => RetentionDays > 0;
}
