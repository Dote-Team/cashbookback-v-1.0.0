namespace cashbook.Helper;

public sealed record AuditFieldChange(string Field, string? OldValue, string? NewValue);
