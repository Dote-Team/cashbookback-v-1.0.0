using System;
using System.Threading.Tasks;
using cashbook.Dto;
using cashbook.Dto.Trash;
using cashbook.Models;

namespace cashbook.Interfaces;

public interface ITrashRepository
{
	Task<PaginatedResponse<TrashItemDto>> GetTrashAsync(Guid? businessId, Guid? deletedByUserId, int? skip = 1, int? take = 25, string? search = null);

	Task<TrashItem?> GetTrashItemAsync(Guid trashId);


}
