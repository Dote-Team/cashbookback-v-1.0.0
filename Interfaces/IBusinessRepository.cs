using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using cashbook.Dto;
using cashbook.Dto.business;
using cashbook.Models;

namespace cashbook.Interfaces;

public interface IBusinessRepository : IRepository<Business>
{
	Task<Guid> CreateBussinessAsync(CreateBusinessDto createBusinessDto);

	/// <param name="accessibleBookIds">خزائن المستخدم، أو null للسوبر أدمن (كل شيء).</param>
	Task<PaginatedResponse<BusinessDto>> GetPaginatedBusinessesAsync(List<Guid> businessIds, List<Guid>? accessibleBookIds, string role, int? skip = 1, int? take = 25, string search = null);

	Task<BusinessWithBooksDto?> GetBusinessWithBooksDtoByIdAsync(Guid id, List<Guid>? accessibleBookIds);

	Task<bool> ArchiveBusinessAsync(Guid businessId, Guid archivedByUserId, string? reason = null);
}
