using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Constants;
using cashbook.Services;

namespace cashbook.Data.Interceptors;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
	private readonly IAuditLogger _logger;

	private readonly AuditSettings _settings;

	private readonly AsyncLocal<List<AuditLog>?> _pending = new AsyncLocal<List<AuditLog>>();

	public AuditSaveChangesInterceptor(IAuditLogger logger, IOptions<AuditSettings> settings)
	{
		_logger = logger;
		_settings = settings.Value;
	}

	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		Capture(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default(CancellationToken))
	{
		Capture(eventData.Context);
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
	{
		Publish();
		return base.SavedChanges(eventData, result);
	}

	public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default(CancellationToken))
	{
		Publish();
		return base.SavedChangesAsync(eventData, result, cancellationToken);
	}

	public override void SaveChangesFailed(DbContextErrorEventData eventData)
	{
		Discard();
		base.SaveChangesFailed(eventData);
	}

	public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default(CancellationToken))
	{
		Discard();
		return base.SaveChangesFailedAsync(eventData, cancellationToken);
	}

	private void Capture(DbContext? context)
	{
		_pending.Value = null;
		if (!_settings.Enabled || context == null)
		{
			return;
		}
		AuditScope current = AuditScope.Current;
		int num = ((_settings.MaxChangesPerSave > 0) ? _settings.MaxChangesPerSave : 200);
		List<AuditLog> list = new List<AuditLog>();
		int num2 = 0;
		foreach (EntityEntry item in context.ChangeTracker.Entries())
		{
			EntityState state = item.State;
			bool flag = (uint)(state - 2) <= 2u;
			if (!flag || item.Entity is AuditLog)
			{
				continue;
			}
			AuditLog auditLog = Build(item);
			if (auditLog != null)
			{
				if (list.Count >= num)
				{
					num2++;
				}
				else
				{
					list.Add(auditLog);
				}
			}
		}
		if (num2 > 0)
		{
			list.Add(BuildTruncationNotice(num2));
			if (current != null)
			{
				current.ChangesTruncated = true;
			}
		}
		if (list.Count > 0)
		{
			_pending.Value = list;
		}
	}

	private void Publish()
	{
		List<AuditLog> value = _pending.Value;
		_pending.Value = null;
		if (value == null || value.Count == 0)
		{
			return;
		}
		AuditScope current = AuditScope.Current;
		if (current != null)
		{
			for (int i = 0; i < value.Count; i++)
			{
				current.RecordChange();
			}
		}
		try
		{
			_logger.EnqueueRange(value);
		}
		catch
		{
		}
	}

	private void Discard()
	{
		_pending.Value = null;
	}

	private static AuditLog? Build(EntityEntry entry)
	{
		EntityState state = entry.State;
		if (1 == 0)
		{
		}
		string text = state switch
		{
			EntityState.Added => "Added", 
			EntityState.Modified => "Modified", 
			EntityState.Deleted => "Deleted", 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		string text2 = text;
		if (text2 == null)
		{
			return null;
		}
		string name = entry.Metadata.ClrType.Name;
		List<AuditFieldChange> list = CollectChanges(entry, text2);
		if (list.Count == 0)
		{
			return null;
		}
		AuditScope current = AuditScope.Current;
		return new AuditLog
		{
			OccurredAt = DateTime.UtcNow,
			CorrelationId = (current?.CorrelationId ?? Guid.NewGuid()),
			Category = "Data",
			Action = (current?.ResolveDataAction(name, text2) ?? AuditAction.Generic(name.ToLowerInvariant(), "change")),
			Severity = DataSeverity(name, text2),
			Operation = text2,
			EntityName = name,
			EntityId = ReadPrimaryKey(entry),
			BookId = ReadGuidProperty(entry, "BookId"),
			BusinessId = (ReadGuidProperty(entry, "BusinessId") ?? current?.BusinessId),
			UserId = current?.UserId,
			Username = current?.ActorName,
			SessionId = current?.SessionId,
			DeviceToken = current?.DeviceToken,
			IpAddress = current?.IpAddress,
			UserAgent = current?.UserAgent,
			HttpMethod = current?.HttpMethod,
			Path = current?.Path,
			StatusCode = null,
			IsSuccess = true,
			Summary = AuditChangeSet.Build(name, text2, list),
			DataJson = AuditChangeSet.ToJson(list)
		};
	}

	private static List<AuditFieldChange> CollectChanges(EntityEntry entry, string operation)
	{
		List<AuditFieldChange> list = new List<AuditFieldChange>();
		foreach (PropertyEntry property in entry.Properties)
		{
			if (property.Metadata.IsPrimaryKey())
			{
				continue;
			}
			string name = property.Metadata.Name;
			if (string.Equals(name, "UpdatedAt", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (!(operation == "Added"))
			{
				if (operation == "Deleted")
				{
					list.Add(new AuditFieldChange(name, Masked(name, property.OriginalValue), null));
				}
				else if (property.IsModified)
				{
					string a = SensitiveDataMasker.ToInvariantString(property.OriginalValue);
					string b = SensitiveDataMasker.ToInvariantString(property.CurrentValue);
					if (!string.Equals(a, b, StringComparison.Ordinal))
					{
						list.Add(new AuditFieldChange(name, Masked(name, property.OriginalValue), Masked(name, property.CurrentValue)));
					}
				}
			}
			else
			{
				list.Add(new AuditFieldChange(name, null, Masked(name, property.CurrentValue)));
			}
		}
		return list;
	}

	private static string? Masked(string fieldName, object? value)
	{
		string value2 = SensitiveDataMasker.ToInvariantString(value);
		return SensitiveDataMasker.MaskValue(fieldName, value2);
	}

	private static string DataSeverity(string entityName, string operation)
	{
		if (string.Equals(entityName, "BusinessUser", StringComparison.Ordinal))
		{
			return "Critical";
		}
		if (operation == "Deleted")
		{
			if (1 == 0)
			{
			}
			string result;
			switch (entityName)
			{
			case "User":
			case "Business":
			case "Book":
				result = "Critical";
				break;
			default:
				result = "Warning";
				break;
			}
			if (1 == 0)
			{
			}
			return result;
		}
		if (string.Equals(entityName, "User", StringComparison.Ordinal))
		{
			return "Warning";
		}
		return "Info";
	}

	private static AuditLog BuildTruncationNotice(int skipped)
	{
		AuditScope current = AuditScope.Current;
		return new AuditLog
		{
			OccurredAt = DateTime.UtcNow,
			CorrelationId = (current?.CorrelationId ?? Guid.NewGuid()),
			Category = "System",
			Action = "audit.truncated",
			Severity = "Warning",
			IsSuccess = true,
			UserId = current?.UserId,
			Username = current?.ActorName,
			SessionId = current?.SessionId,
			IpAddress = current?.IpAddress,
			Path = current?.Path,
			HttpMethod = current?.HttpMethod,
			Summary = $"عملية واحدة أنتجت {skipped} تغييرا\u064b إضافيا\u064b تجاوز الحد المسموح، ولم ت\u064fسج\u064e\u0651ل تفاصيله",
			DataJson = $"{{\"skipped\":{skipped}}}"
		};
	}

	private static string? ReadPrimaryKey(EntityEntry entry)
	{
		PropertyEntry propertyEntry = entry.Properties.FirstOrDefault((PropertyEntry p) => p.Metadata.IsPrimaryKey());
		if (propertyEntry == null)
		{
			return null;
		}
		return (propertyEntry.CurrentValue ?? propertyEntry.OriginalValue)?.ToString();
	}

	private static Guid? ReadGuidProperty(EntityEntry entry, string propertyName)
	{
		return (entry.Properties.FirstOrDefault((PropertyEntry p) => string.Equals(p.Metadata.Name, propertyName, StringComparison.Ordinal))?.CurrentValue is Guid guid && guid != Guid.Empty) ? new Guid?(guid) : ((Guid?)null);
	}
}
