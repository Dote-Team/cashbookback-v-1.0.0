using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.audit;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Constants;

namespace cashbook.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
	private const int MaxPageSize = 200;

	public const int MaxExportRows = 20000;

	private readonly ApplicationDbContext _context;

	private static readonly Expression<Func<AuditLog, AuditLogDetailDto>> DetailProjection = (AuditLog a) => new AuditLogDetailDto
	{
		Id = a.Id,
		OccurredAt = a.OccurredAt,
		CorrelationId = a.CorrelationId,
		Category = a.Category,
		Action = a.Action,
		Severity = a.Severity,
		Summary = a.Summary,
		IsSuccess = a.IsSuccess,
		StatusCode = a.StatusCode,
		UserId = a.UserId,
		Username = a.Username,
		BusinessId = a.BusinessId,
		BookId = a.BookId,
		EntityName = a.EntityName,
		EntityId = a.EntityId,
		Operation = a.Operation,
		HttpMethod = a.HttpMethod,
		Path = a.Path,
		IpAddress = a.IpAddress,
		DurationMs = a.DurationMs,
		IsSlow = a.IsSlow,
		QueryString = a.QueryString,
		DataJson = a.DataJson,
		Error = a.Error,
		UserAgent = a.UserAgent,
		SessionId = a.SessionId,
		DeviceToken = a.DeviceToken
	};

	public AuditLogRepository(ApplicationDbContext context)
	{
		_context = context;
	}

	public async Task<PaginatedResponse<AuditLogListItemDto>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default(CancellationToken))
	{
		IQueryable<AuditLog> filtered = ApplyFilters(_context.AuditLogs.AsNoTracking(), query);
		int total = await filtered.CountAsync(cancellationToken);
		int take = Math.Clamp(query.Take, 1, 200);
		int skip = Math.Max(0, query.Skip);
		List<AuditLogListItemDto> rows = await (from a in ApplySorting(filtered, query).Skip(skip).Take(take)
			select new AuditLogListItemDto
			{
				Id = a.Id,
				OccurredAt = a.OccurredAt,
				CorrelationId = a.CorrelationId,
				Category = a.Category,
				Action = a.Action,
				Severity = a.Severity,
				Summary = a.Summary,
				IsSuccess = a.IsSuccess,
				StatusCode = a.StatusCode,
				UserId = a.UserId,
				Username = a.Username,
				BusinessId = a.BusinessId,
				BookId = a.BookId,
				EntityName = a.EntityName,
				EntityId = a.EntityId,
				Operation = a.Operation,
				HttpMethod = a.HttpMethod,
				Path = a.Path,
				IpAddress = a.IpAddress,
				DurationMs = a.DurationMs,
				IsSlow = a.IsSlow
			}).ToListAsync(cancellationToken);
		foreach (AuditLogListItemDto row in rows)
		{
			Enrich(row);
		}
		return new PaginatedResponse<AuditLogListItemDto>
		{
			Data = rows,
			TotalRecords = total,
			Skip = skip,
			Take = take
		};
	}

	public async Task<AuditLogDetailDto?> GetByIdAsync(long id, AuditLogQuery scope, CancellationToken cancellationToken = default(CancellationToken))
	{
		AuditLogDetailDto row = await (from a in ApplyScope(_context.AuditLogs.AsNoTracking(), scope)
			where a.Id == id
			select a).Select(DetailProjection).FirstOrDefaultAsync(cancellationToken);
		if (row != null)
		{
			EnrichDetail(row);
		}
		return row;
	}

	public async Task<List<AuditLogDetailDto>> GetTraceAsync(Guid correlationId, AuditLogQuery scope, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<AuditLogDetailDto> rows = await (from a in ApplyScope(_context.AuditLogs.AsNoTracking(), scope)
			where a.CorrelationId == correlationId
			orderby a.OccurredAt, a.Id
			select a).Select(DetailProjection).ToListAsync(cancellationToken);
		foreach (AuditLogDetailDto row in rows)
		{
			EnrichDetail(row);
		}
		return rows;
	}

	public async Task<List<AuditLogDetailDto>> ExportAsync(AuditLogQuery query, int maxRows, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<AuditLogDetailDto> rows = await Queryable.Take(count: Math.Clamp(maxRows, 1, 20000), source: ApplySorting(ApplyFilters(_context.AuditLogs.AsNoTracking(), query), query)).Select(DetailProjection).ToListAsync(cancellationToken);
		foreach (AuditLogDetailDto row in rows)
		{
			EnrichDetail(row);
		}
		return rows;
	}

	public async Task<AuditStatsDto> GetStatsAsync(AuditLogQuery query, CancellationToken cancellationToken = default(CancellationToken))
	{
		IQueryable<AuditLog> filtered = ApplyFilters(_context.AuditLogs.AsNoTracking(), query);
		AuditStatsDto stats = new AuditStatsDto
		{
			From = (query.From ?? DateTime.UtcNow.AddDays(-30.0)),
			To = (query.To ?? DateTime.UtcNow),
			RangeDays = Math.Max(1, (int)((query.To ?? DateTime.UtcNow) - (query.From ?? DateTime.UtcNow.AddDays(-30.0))).TotalDays)
		};
		AuditStatsDto auditStatsDto = stats;
		auditStatsDto.Total = await filtered.CountAsync(cancellationToken);
		AuditStatsDto auditStatsDto2 = stats;
		auditStatsDto2.Failures = await filtered.CountAsync((AuditLog a) => !a.IsSuccess, cancellationToken);
		AuditStatsDto auditStatsDto3 = stats;
		auditStatsDto3.SecurityEvents = await filtered.CountAsync((AuditLog a) => a.StatusCode == (int?)401 || a.StatusCode == (int?)403, cancellationToken);
		AuditStatsDto auditStatsDto4 = stats;
		auditStatsDto4.CriticalEvents = await filtered.CountAsync((AuditLog a) => a.Severity == "Critical", cancellationToken);
		AuditStatsDto auditStatsDto5 = stats;
		auditStatsDto5.SlowRequests = await filtered.CountAsync((AuditLog a) => a.IsSlow, cancellationToken);
		AuditStatsDto auditStatsDto6 = stats;
		auditStatsDto6.DistinctUsers = await (from a in filtered
			where a.UserId != null
			select a.UserId).Distinct().CountAsync(cancellationToken);
		stats.AverageDurationMs = Math.Round((await filtered.Where((AuditLog a) => a.DurationMs != (long?)null).Select((AuditLog a) => (double?)a.DurationMs).AverageAsync(cancellationToken)).GetValueOrDefault(), 1);
		AuditStatsDto auditStatsDto7 = stats;
		auditStatsDto7.ByCategory = await BuildBreakdown(from a in filtered
			group a by a.Category, (string key) => AuditCategory.Label(key), cancellationToken);
		AuditStatsDto auditStatsDto8 = stats;
		auditStatsDto8.BySeverity = await BuildBreakdown(from a in filtered
			group a by a.Severity, (string key) => AuditSeverity.Label(key), cancellationToken);
		AuditStatsDto auditStatsDto9 = stats;
		auditStatsDto9.ByAction = await BuildBreakdown(from a in filtered
			group a by a.Action, (string key) => AuditNarrator.DescribeAction(key), cancellationToken, 15);
		AuditStatsDto auditStatsDto10 = stats;
		auditStatsDto10.ByUser = await BuildBreakdown(from a in filtered
			where a.Username != null
			group a by a.Username, (string key) => key, cancellationToken, 15);
		AuditStatsDto auditStatsDto11 = stats;
		auditStatsDto11.Daily = await (from a in filtered
			group a by a.OccurredAt.Date into g
			select new AuditDailyPointDto
			{
				Date = g.Key,
				Total = g.Count(),
				Failures = g.Count((AuditLog x) => !x.IsSuccess)
			} into p
			orderby p.Date
			select p).ToListAsync(cancellationToken);
		return stats;
	}

	public async Task<(long TotalRows, DateTime? Oldest, DateTime? Newest)> GetTableInfoAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		IQueryable<AuditLog> table = _context.AuditLogs.AsNoTracking();
		long total = await table.LongCountAsync(cancellationToken);
		if (total == 0)
		{
			return (TotalRows: 0L, Oldest: null, Newest: null);
		}
		return (TotalRows: total, Oldest: await table.MinAsync((AuditLog a) => (DateTime?)a.OccurredAt, cancellationToken), Newest: await table.MaxAsync((AuditLog a) => (DateTime?)a.OccurredAt, cancellationToken));
	}

	private static async Task<List<AuditBreakdownItemDto>> BuildBreakdown(IQueryable<IGrouping<string, AuditLog>> grouped, Func<string, string> labelFactory, CancellationToken cancellationToken, int? take = null)
	{
		IOrderedQueryable<AuditBreakdownItemDto> query = from g in grouped
			select new AuditBreakdownItemDto
			{
				Key = g.Key,
				Count = g.Count()
			} into auditBreakdownItemDto
			orderby auditBreakdownItemDto.Count descending
			select auditBreakdownItemDto;
		if (take.HasValue)
		{
			query = (IOrderedQueryable<AuditBreakdownItemDto>)query.Take(take.Value);
		}
		List<AuditBreakdownItemDto> items = await query.ToListAsync(cancellationToken);
		foreach (AuditBreakdownItemDto item in items)
		{
			item.Label = labelFactory(item.Key);
		}
		return items;
	}

	private static IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> source, AuditLogQuery query)
	{
		source = ApplyScope(source, query);
		if (query.From.HasValue)
		{
			source = source.Where((AuditLog a) => a.OccurredAt >= query.From.Value);
		}
		if (query.To.HasValue)
		{
			source = source.Where((AuditLog a) => a.OccurredAt <= query.To.Value);
		}
		if (query.UserId.HasValue)
		{
			source = source.Where((AuditLog a) => a.UserId == query.UserId.Value);
		}
		if (query.SessionId.HasValue)
		{
			source = source.Where((AuditLog a) => a.SessionId == query.SessionId.Value);
		}
		if (query.CorrelationId.HasValue)
		{
			source = source.Where((AuditLog a) => a.CorrelationId == query.CorrelationId.Value);
		}
		if (query.BusinessId.HasValue)
		{
			source = source.Where((AuditLog a) => a.BusinessId == query.BusinessId.Value);
		}
		if (query.BookId.HasValue)
		{
			source = source.Where((AuditLog a) => a.BookId == query.BookId.Value);
		}
		if (!string.IsNullOrWhiteSpace(query.Username))
		{
			string username = query.Username.Trim();
			source = source.Where((AuditLog a) => a.Username != null && a.Username.Contains(username));
		}
		if (!string.IsNullOrWhiteSpace(query.IpAddress))
		{
			string ip = query.IpAddress.Trim();
			source = source.Where((AuditLog a) => a.IpAddress == ip);
		}
		if (!string.IsNullOrWhiteSpace(query.Category))
		{
			string category = query.Category.Trim();
			source = source.Where((AuditLog a) => a.Category == category);
		}
		if (!string.IsNullOrWhiteSpace(query.Action))
		{
			string action = query.Action.Trim();
			string prefix = action + ".";
			source = source.Where((AuditLog a) => a.Action == action || a.Action.StartsWith(prefix));
		}
		if (!string.IsNullOrWhiteSpace(query.EntityName))
		{
			string entity = query.EntityName.Trim();
			source = source.Where((AuditLog a) => a.EntityName == entity);
		}
		if (!string.IsNullOrWhiteSpace(query.EntityId))
		{
			string entityId = query.EntityId.Trim();
			source = source.Where((AuditLog a) => a.EntityId == entityId);
		}
		if (query.IsSuccess.HasValue)
		{
			source = source.Where((AuditLog a) => a.IsSuccess == query.IsSuccess.Value);
		}
		if (query.OnlyFailures == true)
		{
			source = source.Where((AuditLog a) => !a.IsSuccess);
		}
		if (query.OnlySlow == true)
		{
			source = source.Where((AuditLog a) => a.IsSlow);
		}
		if (query.OnlySecurityEvents == true)
		{
			source = source.Where((AuditLog a) => a.StatusCode == (int?)401 || a.StatusCode == (int?)403);
		}
		if (!string.IsNullOrWhiteSpace(query.Severity))
		{
			int num = AuditSeverity.Rank(query.Severity);
			if (1 == 0)
			{
			}
			string[] array = num switch
			{
				3 => new string[1] { "Critical" }, 
				2 => new string[2] { "Critical", "Warning" }, 
				_ => new string[3] { "Critical", "Warning", "Info" }, 
			};
			if (1 == 0)
			{
			}
			string[] allowed = array;
			source = source.Where((AuditLog a) => Enumerable.Contains(allowed, a.Severity));
		}
		if (!string.IsNullOrWhiteSpace(query.Search))
		{
			string term = query.Search.Trim();
			source = source.Where((AuditLog a) => (a.Summary != null && a.Summary.Contains(term)) || (a.Username != null && a.Username.Contains(term)) || (a.Path != null && a.Path.Contains(term)) || (a.Action != null && a.Action.Contains(term)) || (a.EntityId != null && a.EntityId.Contains(term)));
		}
		return source;
	}

	private static IQueryable<AuditLog> ApplyScope(IQueryable<AuditLog> source, AuditLogQuery scope)
	{
		if (scope.IsUnrestricted)
		{
			return source;
		}
		Guid? businessId = scope.ScopeBusinessId;
		List<Guid> scopeBookIds = scope.ScopeBookIds;
		if (!businessId.HasValue && (scopeBookIds == null || scopeBookIds.Count == 0))
		{
			return source.Where((AuditLog a) => false);
		}
		List<Guid> books = scopeBookIds ?? new List<Guid>();
		return source.Where((AuditLog a) => (businessId != null && a.BusinessId == businessId) || (a.BookId != null && books.Contains(a.BookId.Value)));
	}

	private static IQueryable<AuditLog> ApplySorting(IQueryable<AuditLog> source, AuditLogQuery query)
	{
		bool flag = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
		string text = query.SortBy?.ToLowerInvariant();
		if (1 == 0)
		{
		}
		IOrderedQueryable<AuditLog> result = text switch
		{
			"duration" => flag ? (from a in source
				orderby a.DurationMs descending, a.Id descending
				select a) : (from a in source
				orderby a.DurationMs, a.Id
				select a), 
			"severity" => flag ? (from a in source
				orderby a.Severity descending, a.Id descending
				select a) : (from a in source
				orderby a.Severity, a.Id
				select a), 
			"user" => flag ? (from a in source
				orderby a.Username descending, a.Id descending
				select a) : (from a in source
				orderby a.Username, a.Id
				select a), 
			_ => flag ? (from a in source
				orderby a.OccurredAt descending, a.Id descending
				select a) : (from a in source
				orderby a.OccurredAt, a.Id
				select a), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static void Enrich(AuditLogListItemDto row)
	{
		row.CategoryLabel = AuditCategory.Label(row.Category);
		row.SeverityLabel = AuditSeverity.Label(row.Severity);
		row.ActionLabel = AuditNarrator.DescribeAction(row.Action);
		row.EntityLabel = ((row.EntityName == null) ? null : AuditNarrator.LabelEntity(row.EntityName));
		row.OperationLabel = ((row.Operation == null) ? null : AuditOperation.Label(row.Operation));
	}

	private static void EnrichDetail(AuditLogDetailDto row)
	{
		Enrich(row);
		row.DeviceToken = MaskDeviceToken(row.DeviceToken);
	}

	private static string? MaskDeviceToken(string? deviceToken)
	{
		if (string.IsNullOrWhiteSpace(deviceToken))
		{
			return deviceToken;
		}
		if (deviceToken.Length <= 12)
		{
			return deviceToken;
		}
		return deviceToken.Substring(0, 8) + "…" + deviceToken.Substring(deviceToken.Length - 4);
	}
}
