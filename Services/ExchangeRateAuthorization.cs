using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Helper;
using cashbook.Models;

namespace cashbook.Services;

public class ExchangeRateAuthorization : IExchangeRateAuthorization
{
	private sealed class MembershipInfo
	{
		public string Role { get; init; }

		public List<Guid>? BookIds { get; init; }
	}

	private readonly ApplicationDbContext _context;

	public ExchangeRateAuthorization(ApplicationDbContext context)
	{
		_context = context;
	}

	public async Task<bool> IsSuperAdminAsync(Guid userId)
	{
		return await (from u in _context.Users
			where u.Id == userId
			select u.IsSuperAdmin).FirstOrDefaultAsync();
	}

	public async Task<bool> CanManageAsync(Guid userId, Guid bookId)
	{
		if (await IsSuperAdminAsync(userId))
		{
			return true;
		}
		Guid? businessId = await GetBookBusinessIdAsync(bookId);
		if (!businessId.HasValue)
		{
			return false;
		}
		MembershipInfo membership = await GetMembershipAsync(userId, businessId.Value);
		if (membership == null)
		{
			return false;
		}
		if (Roles.CanManageAllExchangeRates(membership.Role))
		{
			return true;
		}
		return Roles.CanManageExchangeRate(membership.Role, membership.BookIds, bookId);
	}

	public async Task<bool> CanViewAsync(Guid userId, Guid bookId)
	{
		if (await IsSuperAdminAsync(userId))
		{
			return true;
		}
		Guid? businessId = await GetBookBusinessIdAsync(bookId);
		if (!businessId.HasValue)
		{
			return false;
		}
		MembershipInfo membership = await GetMembershipAsync(userId, businessId.Value);
		if (membership == null)
		{
			return false;
		}
		string role = membership.Role?.ToLowerInvariant();
		if (Roles.IsPrivileged(role) || Roles.CanManageAllExchangeRates(role))
		{
			return true;
		}
		return Roles.IsBookScoped(role) && membership.BookIds != null && membership.BookIds.Contains(bookId);
	}

	public async Task<List<Guid>> GetManagedBookIdsAsync(Guid userId, Guid businessId)
	{
		IQueryable<Guid> allBookIdsInBusiness = from b in _context.Books
			where b.BusinessId == businessId
			select b.Id;
		if (await IsSuperAdminAsync(userId))
		{
			return await allBookIdsInBusiness.ToListAsync();
		}
		MembershipInfo membership = await GetMembershipAsync(userId, businessId);
		if (membership == null)
		{
			return new List<Guid>();
		}
		if (Roles.CanManageAllExchangeRates(membership.Role))
		{
			return await allBookIdsInBusiness.ToListAsync();
		}
		if (!string.Equals(membership.Role, "portfolio_manager", StringComparison.OrdinalIgnoreCase))
		{
			return new List<Guid>();
		}
		List<Guid> assigned = membership.BookIds ?? new List<Guid>();
		return await (from b in _context.Books
			where b.BusinessId == businessId && assigned.Contains(b.Id)
			select b.Id).ToListAsync();
	}

	public async Task<List<Guid>> GetManageableBusinessIdsAsync(Guid userId)
	{
		if (await IsSuperAdminAsync(userId))
		{
			return await _context.Businesses.Select((Business b) => b.Id).ToListAsync();
		}
		return await (from bu in _context.BusinessUsers
			where bu.UserId == userId && bu.Role.ToLower() == "portfolio_manager"
			select bu.BusinessId).Distinct().ToListAsync();
	}

	private async Task<Guid?> GetBookBusinessIdAsync(Guid bookId)
	{
		return await _context.Books.Where((Book b) => b.Id == bookId).Select((Expression<Func<Book, Guid?>>)((Book b) => b.BusinessId)).FirstOrDefaultAsync();
	}

	private async Task<MembershipInfo?> GetMembershipAsync(Guid userId, Guid businessId)
	{
		return await (from bu in _context.BusinessUsers
			where bu.UserId == userId && bu.BusinessId == businessId
			select new MembershipInfo
			{
				Role = bu.Role.ToLower(),
				BookIds = bu.BookIds
			}).FirstOrDefaultAsync();
	}
}
