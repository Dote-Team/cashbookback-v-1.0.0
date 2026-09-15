using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cashbook.Dto;
using cashbook.Dto.audit;

namespace cashbook.Interfaces;

public interface IAuditLogRepository
{
	Task<PaginatedResponse<AuditLogListItemDto>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default(CancellationToken));

	Task<AuditLogDetailDto?> GetByIdAsync(long id, AuditLogQuery scope, CancellationToken cancellationToken = default(CancellationToken));

	Task<List<AuditLogDetailDto>> GetTraceAsync(Guid correlationId, AuditLogQuery scope, CancellationToken cancellationToken = default(CancellationToken));

	Task<List<AuditLogDetailDto>> ExportAsync(AuditLogQuery query, int maxRows, CancellationToken cancellationToken = default(CancellationToken));

	Task<AuditStatsDto> GetStatsAsync(AuditLogQuery query, CancellationToken cancellationToken = default(CancellationToken));

	Task<(long TotalRows, DateTime? Oldest, DateTime? Newest)> GetTableInfoAsync(CancellationToken cancellationToken = default(CancellationToken));
}
