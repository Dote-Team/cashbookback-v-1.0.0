using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cashbook.Services;

public interface IExchangeRateAuthorization
{
	Task<bool> IsSuperAdminAsync(Guid userId);

	Task<bool> CanManageAsync(Guid userId, Guid bookId);

	Task<bool> CanViewAsync(Guid userId, Guid bookId);

	Task<List<Guid>> GetManagedBookIdsAsync(Guid userId, Guid businessId);

	Task<List<Guid>> GetManageableBusinessIdsAsync(Guid userId);
}
