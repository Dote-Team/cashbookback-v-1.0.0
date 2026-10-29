using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Helper;
using cashbook.Models;

namespace cashbook.Services;

public enum AccessStatus
{
	Granted,
	NotFound,
	Forbidden
}

public sealed record BookAccess(AccessStatus Status, Guid BusinessId);

public sealed record UserAccessInfo(Guid Id, int Level, bool IsActive, Guid? ParentUserId)
{
	public bool IsSuperAdmin => Level == UserLevels.SuperAdmin;
}

/// <summary>
/// المرجع الوحيد لسؤال «هل يملك هذا المستخدم هذه الصلاحية على هذه الخزنة؟».
/// المنشأة تُستخرج دائماً من الخزنة أو السجل نفسه، ولا يُعتمد على businessId القادم من الطلب.
/// </summary>
public interface IPermissionService
{
	Task<UserAccessInfo?> GetUserAsync(Guid userId);

	Task<bool> IsSuperAdminAsync(Guid userId);

	Task<HashSet<string>> GetBookPermissionsAsync(Guid userId, Guid bookId);

	Task<bool> HasAsync(Guid userId, Guid bookId, string permission);

	/// <summary>يفحص الخزنة: غير موجودة أو مؤرشفة = NotFound، بلا صلاحية = Forbidden.</summary>
	Task<BookAccess> CheckBookAsync(Guid userId, Guid bookId, string permission);

	/// <summary>الصلاحية على أي خزنة في المنشأة؛ للبيانات المرجعية المشتركة على مستوى المنشأة.</summary>
	Task<bool> HasInBusinessAsync(Guid userId, Guid businessId, string permission);

	/// <summary>هل للمستخدم أي صلاحية على أي خزنة في المنشأة؟</summary>
	Task<bool> IsBusinessMemberAsync(Guid userId, Guid businessId);

	/// <summary>الخزائن التي يملك عليها الصلاحية المحددة (أو أي صلاحية إن كانت null).</summary>
	Task<List<Guid>> GetBookIdsAsync(Guid userId, Guid businessId, string? permission = null);

	Task<List<Guid>> GetBusinessIdsAsync(Guid userId);

	void Invalidate();
}

public sealed class PermissionService : IPermissionService
{
	private readonly ApplicationDbContext _context;

	// ذاكرة على مستوى الطلب الواحد: الخدمة Scoped، فلا تتسرب صلاحيات بين الطلبات.
	private readonly Dictionary<Guid, UserAccessInfo?> _users = new();

	private readonly Dictionary<(Guid, Guid), HashSet<string>> _bookPermissions = new();

	public PermissionService(ApplicationDbContext context)
	{
		_context = context;
	}

	public void Invalidate()
	{
		_users.Clear();
		_bookPermissions.Clear();
	}

	public async Task<UserAccessInfo?> GetUserAsync(Guid userId)
	{
		if (_users.TryGetValue(userId, out UserAccessInfo? cached))
		{
			return cached;
		}
		var row = await _context.Users.AsNoTracking()
			.Where(u => u.Id == userId)
			.Select(u => new { u.Id, u.Level, u.IsSuperAdmin, u.IsActive, u.ParentUserId })
			.FirstOrDefaultAsync();
		UserAccessInfo? info = row == null
			? null
			: new UserAccessInfo(row.Id, row.IsSuperAdmin ? UserLevels.SuperAdmin : row.Level, row.IsActive, row.ParentUserId);
		_users[userId] = info;
		return info;
	}

	public async Task<bool> IsSuperAdminAsync(Guid userId)
	{
		UserAccessInfo? user = await GetUserAsync(userId);
		return user is { IsActive: true, IsSuperAdmin: true };
	}

	public async Task<HashSet<string>> GetBookPermissionsAsync(Guid userId, Guid bookId)
	{
		if (_bookPermissions.TryGetValue((userId, bookId), out HashSet<string>? cached))
		{
			return cached;
		}
		HashSet<string> result;
		UserAccessInfo? user = await GetUserAsync(userId);
		if (user == null || !user.IsActive)
		{
			result = new HashSet<string>();
		}
		else if (user.IsSuperAdmin)
		{
			bool bookExists = await _context.Books.AnyAsync(b => b.Id == bookId);
			result = bookExists ? new HashSet<string>(Permissions.All) : new HashSet<string>();
		}
		else
		{
			// الانضمام إلى Books يطبّق فلتر الأرشفة: لا صلاحية على خزنة مؤرشفة.
			List<string> codes = await (from p in _context.UserWalletPermissions.AsNoTracking()
				join b in _context.Books on p.BookId equals b.Id
				where p.UserId == userId && p.BookId == bookId
				select p.Permission).ToListAsync();
			result = codes.ToHashSet();
		}
		_bookPermissions[(userId, bookId)] = result;
		return result;
	}

	public async Task<bool> HasAsync(Guid userId, Guid bookId, string permission)
	{
		return (await GetBookPermissionsAsync(userId, bookId)).Contains(permission);
	}

	public async Task<BookAccess> CheckBookAsync(Guid userId, Guid bookId, string permission)
	{
		if (bookId == Guid.Empty)
		{
			return new BookAccess(AccessStatus.NotFound, Guid.Empty);
		}
		Guid? businessId = await _context.Books.AsNoTracking()
			.Where(b => b.Id == bookId)
			.Select(b => (Guid?)b.BusinessId)
			.FirstOrDefaultAsync();
		if (!businessId.HasValue)
		{
			return new BookAccess(AccessStatus.NotFound, Guid.Empty);
		}
		bool granted = await HasAsync(userId, bookId, permission);
		return new BookAccess(granted ? AccessStatus.Granted : AccessStatus.Forbidden, businessId.Value);
	}

	public async Task<bool> HasInBusinessAsync(Guid userId, Guid businessId, string permission)
	{
		UserAccessInfo? user = await GetUserAsync(userId);
		if (user == null || !user.IsActive)
		{
			return false;
		}
		if (user.IsSuperAdmin)
		{
			return await _context.Businesses.AnyAsync(b => b.Id == businessId);
		}
		return await (from p in _context.UserWalletPermissions
			join b in _context.Books on p.BookId equals b.Id
			where p.UserId == userId && b.BusinessId == businessId && p.Permission == permission
			select p.Id).AnyAsync();
	}

	public async Task<bool> IsBusinessMemberAsync(Guid userId, Guid businessId)
	{
		UserAccessInfo? user = await GetUserAsync(userId);
		if (user == null || !user.IsActive)
		{
			return false;
		}
		if (user.IsSuperAdmin)
		{
			return await _context.Businesses.AnyAsync(b => b.Id == businessId);
		}
		return await (from p in _context.UserWalletPermissions
			join b in _context.Books on p.BookId equals b.Id
			where p.UserId == userId && b.BusinessId == businessId
			select p.Id).AnyAsync();
	}

	public async Task<List<Guid>> GetBookIdsAsync(Guid userId, Guid businessId, string? permission = null)
	{
		UserAccessInfo? user = await GetUserAsync(userId);
		if (user == null || !user.IsActive)
		{
			return new List<Guid>();
		}
		if (user.IsSuperAdmin)
		{
			return await _context.Books.Where(b => b.BusinessId == businessId).Select(b => b.Id).ToListAsync();
		}
		IQueryable<Guid> query = from p in _context.UserWalletPermissions
			join b in _context.Books on p.BookId equals b.Id
			where p.UserId == userId && b.BusinessId == businessId && (permission == null || p.Permission == permission)
			select b.Id;
		return await query.Distinct().ToListAsync();
	}

	public async Task<List<Guid>> GetBusinessIdsAsync(Guid userId)
	{
		UserAccessInfo? user = await GetUserAsync(userId);
		if (user == null || !user.IsActive)
		{
			return new List<Guid>();
		}
		if (user.IsSuperAdmin)
		{
			return await _context.Businesses.Select(b => b.Id).ToListAsync();
		}
		return await (from p in _context.UserWalletPermissions
			join b in _context.Books on p.BookId equals b.Id
			where p.UserId == userId
			select b.BusinessId).Distinct().ToListAsync();
	}
}
