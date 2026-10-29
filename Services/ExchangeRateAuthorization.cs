using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Helper;

namespace cashbook.Services;

/// <summary>صلاحيات سعر الصرف، مبنية على صلاحية <see cref="Permissions.ExchangeRateManage"/> لكل خزنة.</summary>
public class ExchangeRateAuthorization : IExchangeRateAuthorization
{
	private readonly ApplicationDbContext _context;

	private readonly IPermissionService _permissions;

	public ExchangeRateAuthorization(ApplicationDbContext context, IPermissionService permissions)
	{
		_context = context;
		_permissions = permissions;
	}

	public Task<bool> IsSuperAdminAsync(Guid userId)
	{
		return _permissions.IsSuperAdminAsync(userId);
	}

	public Task<bool> CanManageAsync(Guid userId, Guid bookId)
	{
		return _permissions.HasAsync(userId, bookId, Permissions.ExchangeRateManage);
	}

	public Task<bool> CanViewAsync(Guid userId, Guid bookId)
	{
		return _permissions.HasAsync(userId, bookId, Permissions.BookView);
	}

	public Task<List<Guid>> GetManagedBookIdsAsync(Guid userId, Guid businessId)
	{
		return _permissions.GetBookIdsAsync(userId, businessId, Permissions.ExchangeRateManage);
	}

	public async Task<List<Guid>> GetManageableBusinessIdsAsync(Guid userId)
	{
		if (await _permissions.IsSuperAdminAsync(userId))
		{
			return await _context.Businesses.Select(b => b.Id).ToListAsync();
		}
		UserAccessInfo? user = await _permissions.GetUserAsync(userId);
		if (user == null || !user.IsActive)
		{
			return new List<Guid>();
		}
		return await (from p in _context.UserWalletPermissions
			join b in _context.Books on p.BookId equals b.Id
			where p.UserId == userId && p.Permission == Permissions.ExchangeRateManage
			select b.BusinessId).Distinct().ToListAsync();
	}
}
