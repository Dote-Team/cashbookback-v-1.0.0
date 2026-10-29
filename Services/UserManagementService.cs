using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.userManagement;
using cashbook.Helper;
using cashbook.Models;

namespace cashbook.Services;

public sealed record ServiceResult(bool Ok, HttpStatusCode Status, string? Error = null, object? Result = null)
{
	public static ServiceResult Success(object? result = null) => new(true, HttpStatusCode.OK, null, result);

	public static ServiceResult Fail(HttpStatusCode status, string error) => new(false, status, error);
}

public interface IUserManagementService
{
	Task<ServiceResult> CreateUserAsync(Guid callerId, CreateManagedUserDto dto);

	Task<ServiceResult> SetGrantsAsync(Guid callerId, Guid targetId, List<BookGrantDto> grants);

	Task<ServiceResult> UpdateUserAsync(Guid callerId, Guid targetId, UpdateManagedUserDto dto);

	Task<ServiceResult> DeactivateAsync(Guid callerId, Guid targetId);

	Task<ServiceResult> ActivateAsync(Guid callerId, Guid targetId);

	Task<ServiceResult> ResetPasswordAsync(Guid callerId, Guid targetId, string password);

	Task<PaginatedResponse<ManagedUserDto>> ListAsync(Guid callerId, Guid? businessId, string? search, bool includeInactive, int skip, int take);

	Task<ManagedUserDto?> GetAsync(Guid callerId, Guid targetId);

	/// <summary>هل يستطيع المستدعي إدارة هذا المستخدم (سوبر أدمن، أو هو من أنشأه)؟</summary>
	Task<bool> CanManageAsync(Guid callerId, Guid targetId);
}

/// <summary>
/// قواعد الوراثة كلها هنا:
/// <list type="number">
/// <item>السوبر أدمن ينشئ أدمن، والأدمن ينشئ مستخدماً، والمستخدم لا ينشئ أحداً.</item>
/// <item>لا يُمنح أحد صلاحية لا يملكها مانحها على الخزنة نفسها.</item>
/// <item>صلاحيات المستخدم لا تتجاوز صلاحيات الأدمن الذي أنشأه، حتى لو منحها السوبر أدمن.</item>
/// <item>سحب صلاحية من أدمن يسحبها تلقائياً من مستخدميه، وتعطيله يعطّلهم.</item>
/// </list>
/// </summary>
public sealed class UserManagementService : IUserManagementService
{
	private const int MaxPageSize = 200;

	private readonly ApplicationDbContext _context;

	private readonly IPermissionService _permissions;

	private readonly ISessionCache _sessionCache;

	public UserManagementService(ApplicationDbContext context, IPermissionService permissions, ISessionCache sessionCache)
	{
		_context = context;
		_permissions = permissions;
		_sessionCache = sessionCache;
	}

	public async Task<bool> CanManageAsync(Guid callerId, Guid targetId)
	{
		if (callerId == targetId)
		{
			return false;
		}
		UserAccessInfo? caller = await _permissions.GetUserAsync(callerId);
		UserAccessInfo? target = await _permissions.GetUserAsync(targetId);
		if (caller == null || !caller.IsActive || target == null || target.IsSuperAdmin)
		{
			return false;
		}
		if (caller.IsSuperAdmin)
		{
			return true;
		}
		return caller.Level == UserLevels.Admin && target.ParentUserId == callerId;
	}

	public async Task<ServiceResult> CreateUserAsync(Guid callerId, CreateManagedUserDto dto)
	{
		UserAccessInfo? caller = await _permissions.GetUserAsync(callerId);
		if (caller == null || !caller.IsActive || caller.Level == UserLevels.User)
		{
			return ServiceResult.Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية إنشاء مستخدمين.");
		}
		string? username = dto.Username?.Trim();
		if (string.IsNullOrWhiteSpace(username))
		{
			return ServiceResult.Fail(HttpStatusCode.BadRequest, "اسم المستخدم مطلوب.");
		}
		if (string.IsNullOrWhiteSpace(dto.Name))
		{
			return ServiceResult.Fail(HttpStatusCode.BadRequest, "الاسم مطلوب.");
		}
		if (!PasswordPolicy.IsValid(dto.Password, out string passwordError))
		{
			return ServiceResult.Fail(HttpStatusCode.BadRequest, passwordError);
		}
		if (await _context.Users.AnyAsync(u => u.Username == username))
		{
			return ServiceResult.Fail(HttpStatusCode.Conflict, "اسم المستخدم مستخدم مسبقاً.");
		}
		string email = string.IsNullOrWhiteSpace(dto.Email) ? BuildInternalEmail(username) : dto.Email.Trim();
		if (await _context.Users.AnyAsync(u => u.Email == email))
		{
			return ServiceResult.Fail(HttpStatusCode.Conflict, "البريد الإلكتروني مستخدم مسبقاً.");
		}
		int newLevel = caller.IsSuperAdmin ? UserLevels.Admin : UserLevels.User;
		(Dictionary<Guid, HashSet<string>>? grants, string? grantError) = await ValidateGrantsAsync(caller, newLevel, callerId, dto.Grants);
		if (grantError != null)
		{
			return ServiceResult.Fail(HttpStatusCode.BadRequest, grantError);
		}
		if (grants!.Count == 0)
		{
			return ServiceResult.Fail(HttpStatusCode.BadRequest, "حدد خزنة واحدة على الأقل مع صلاحياتها.");
		}
		User user = new User
		{
			Username = username,
			Name = dto.Name.Trim(),
			Email = email,
			Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
			Level = newLevel,
			ParentUserId = callerId,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_context.Users.Add(user);
		foreach ((Guid bookId, HashSet<string> codes) in grants)
		{
			foreach (string code in codes)
			{
				_context.UserWalletPermissions.Add(new UserWalletPermission
				{
					UserId = user.Id,
					BookId = bookId,
					Permission = code,
					GrantedByUserId = callerId
				});
			}
		}
		await _context.SaveChangesAsync();
		await SyncMembershipsAsync(user.Id, newLevel);
		await _context.SaveChangesAsync();
		_permissions.Invalidate();
		return ServiceResult.Success(new { user.Id });
	}

	public async Task<ServiceResult> SetGrantsAsync(Guid callerId, Guid targetId, List<BookGrantDto> requested)
	{
		if (!await CanManageAsync(callerId, targetId))
		{
			return ServiceResult.Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية إدارة هذا المستخدم.");
		}
		UserAccessInfo caller = (await _permissions.GetUserAsync(callerId))!;
		UserAccessInfo target = (await _permissions.GetUserAsync(targetId))!;
		(Dictionary<Guid, HashSet<string>>? grants, string? grantError) = await ValidateGrantsAsync(caller, target.Level, target.ParentUserId, requested);
		if (grantError != null)
		{
			return ServiceResult.Fail(HttpStatusCode.BadRequest, grantError);
		}
		// نطاق التعديل: السوبر أدمن يستبدل كل صلاحيات المستخدم. الأدمن يستبدل فقط ما على
		// الخزائن التي يملك فيها إدارة المستخدمين، وما سواها يبقى كما هو.
		List<UserWalletPermission> existing = await _context.UserWalletPermissions
			.Where(p => p.UserId == targetId)
			.ToListAsync();
		HashSet<Guid> scope;
		if (caller.IsSuperAdmin)
		{
			scope = existing.Select(p => p.BookId).Concat(grants!.Keys).ToHashSet();
		}
		else
		{
			scope = new HashSet<Guid>();
			foreach (Guid bookId in existing.Select(p => p.BookId).Concat(grants!.Keys).Distinct())
			{
				if (await _permissions.HasAsync(callerId, bookId, Permissions.UsersManage))
				{
					scope.Add(bookId);
				}
			}
		}
		foreach (Guid bookId in scope)
		{
			HashSet<string> wanted = grants!.TryGetValue(bookId, out HashSet<string>? set) ? set : new HashSet<string>();
			ApplyBookGrant(existing.Where(p => p.BookId == bookId).ToList(), targetId, bookId, wanted, callerId);
		}
		await _context.SaveChangesAsync();
		if (target.Level == UserLevels.Admin)
		{
			await CascadeToChildrenAsync(targetId);
		}
		await SyncMembershipsAsync(targetId, target.Level);
		await _context.SaveChangesAsync();
		_permissions.Invalidate();
		return ServiceResult.Success();
	}

	public async Task<ServiceResult> UpdateUserAsync(Guid callerId, Guid targetId, UpdateManagedUserDto dto)
	{
		if (!await CanManageAsync(callerId, targetId))
		{
			return ServiceResult.Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية إدارة هذا المستخدم.");
		}
		User user = await _context.Users.FirstAsync(u => u.Id == targetId);
		if (!string.IsNullOrWhiteSpace(dto.Name))
		{
			user.Name = dto.Name.Trim();
		}
		if (!string.IsNullOrWhiteSpace(dto.Email))
		{
			string email = dto.Email.Trim();
			if (await _context.Users.AnyAsync(u => u.Id != targetId && u.Email == email))
			{
				return ServiceResult.Fail(HttpStatusCode.Conflict, "البريد الإلكتروني مستخدم مسبقاً.");
			}
			user.Email = email;
		}
		user.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();
		return ServiceResult.Success();
	}

	public async Task<ServiceResult> DeactivateAsync(Guid callerId, Guid targetId)
	{
		if (!await CanManageAsync(callerId, targetId))
		{
			return ServiceResult.Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية إدارة هذا المستخدم.");
		}
		// الأدمن يتعطل ومعه كل من أنشأهم.
		List<Guid> affected = new List<Guid> { targetId };
		affected.AddRange(await _context.Users.Where(u => u.ParentUserId == targetId && u.IsActive).Select(u => u.Id).ToListAsync());
		List<User> users = await _context.Users.Where(u => affected.Contains(u.Id)).ToListAsync();
		foreach (User user in users)
		{
			user.IsActive = false;
			user.DeactivatedAt = DateTime.UtcNow;
			user.DeactivatedByUserId = callerId;
			user.UpdatedAt = DateTime.UtcNow;
		}
		_context.Sessions.RemoveRange(await _context.Sessions.Where(s => affected.Contains(s.UserId)).ToListAsync());
		await _context.SaveChangesAsync();
		_sessionCache.InvalidateUsers(affected);
		_permissions.Invalidate();
		return ServiceResult.Success(new { deactivated = affected.Count });
	}

	public async Task<ServiceResult> ActivateAsync(Guid callerId, Guid targetId)
	{
		if (!await CanManageAsync(callerId, targetId))
		{
			return ServiceResult.Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية إدارة هذا المستخدم.");
		}
		User user = await _context.Users.FirstAsync(u => u.Id == targetId);
		if (user.ParentUserId.HasValue)
		{
			UserAccessInfo? parent = await _permissions.GetUserAsync(user.ParentUserId.Value);
			if (parent != null && !parent.IsActive)
			{
				return ServiceResult.Fail(HttpStatusCode.Conflict, "لا يمكن تفعيل المستخدم والأدمن الذي أنشأه معطّل.");
			}
		}
		user.IsActive = true;
		user.DeactivatedAt = null;
		user.DeactivatedByUserId = null;
		user.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();
		_permissions.Invalidate();
		return ServiceResult.Success();
	}

	public async Task<ServiceResult> ResetPasswordAsync(Guid callerId, Guid targetId, string password)
	{
		if (!await CanManageAsync(callerId, targetId))
		{
			return ServiceResult.Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية إدارة هذا المستخدم.");
		}
		if (!PasswordPolicy.IsValid(password, out string passwordError))
		{
			return ServiceResult.Fail(HttpStatusCode.BadRequest, passwordError);
		}
		User user = await _context.Users.FirstAsync(u => u.Id == targetId);
		user.Password = BCrypt.Net.BCrypt.HashPassword(password);
		user.UpdatedAt = DateTime.UtcNow;
		_context.Sessions.RemoveRange(await _context.Sessions.Where(s => s.UserId == targetId).ToListAsync());
		await _context.SaveChangesAsync();
		_sessionCache.InvalidateUsers(new[] { targetId });
		return ServiceResult.Success();
	}

	public async Task<PaginatedResponse<ManagedUserDto>> ListAsync(Guid callerId, Guid? businessId, string? search, bool includeInactive, int skip, int take)
	{
		int page = Math.Max(1, skip);
		int size = Math.Clamp(take, 1, MaxPageSize);
		UserAccessInfo? caller = await _permissions.GetUserAsync(callerId);
		IQueryable<User> query = _context.Users.AsNoTracking().Where(u => !u.IsHidden && u.Level != UserLevels.SuperAdmin && !u.IsSuperAdmin);
		if (caller == null || !caller.IsActive || caller.Level == UserLevels.User)
		{
			query = query.Where(u => false);
		}
		else if (!caller.IsSuperAdmin)
		{
			query = query.Where(u => u.ParentUserId == callerId);
		}
		if (!includeInactive)
		{
			query = query.Where(u => u.IsActive);
		}
		if (businessId.HasValue)
		{
			Guid bid = businessId.Value;
			query = query.Where(u => _context.UserWalletPermissions.Any(p => p.UserId == u.Id && _context.Books.Any(b => b.Id == p.BookId && b.BusinessId == bid)));
		}
		if (!string.IsNullOrWhiteSpace(search))
		{
			string s = search.Trim();
			query = query.Where(u => u.Username.Contains(s) || u.Name.Contains(s) || u.Email.Contains(s));
		}
		int total = await query.CountAsync();
		List<Guid> ids = await query.OrderBy(u => u.Level).ThenBy(u => u.Name).ThenBy(u => u.Id)
			.Skip((page - 1) * size).Take(size).Select(u => u.Id).ToListAsync();
		List<ManagedUserDto> data = await BuildDtosAsync(ids);
		return new PaginatedResponse<ManagedUserDto>
		{
			TotalRecords = total,
			Skip = page,
			Take = size,
			Data = data
		};
	}

	public async Task<ManagedUserDto?> GetAsync(Guid callerId, Guid targetId)
	{
		if (callerId != targetId && !await CanManageAsync(callerId, targetId))
		{
			return null;
		}
		return (await BuildDtosAsync(new List<Guid> { targetId })).FirstOrDefault();
	}

	private async Task<List<ManagedUserDto>> BuildDtosAsync(List<Guid> ids)
	{
		var users = await _context.Users.AsNoTracking()
			.Where(u => ids.Contains(u.Id))
			.Select(u => new
			{
				u.Id, u.Username, u.Name, u.Email, u.Level, u.IsSuperAdmin, u.IsActive, u.ParentUserId, u.CreatedAt,
				ParentName = _context.Users.Where(p => p.Id == u.ParentUserId).Select(p => p.Name).FirstOrDefault(),
				FingerprintCount = _context.UserFingerprints.Count(f => f.UserId == u.Id && f.IsActive)
			}).ToListAsync();
		var grants = await (from p in _context.UserWalletPermissions.AsNoTracking()
			join b in _context.Books on p.BookId equals b.Id
			join bus in _context.Businesses on b.BusinessId equals bus.Id
			where ids.Contains(p.UserId)
			select new { p.UserId, p.BookId, BookName = b.Name, b.BusinessId, BusinessName = bus.Name, p.Permission }).ToListAsync();
		Dictionary<Guid, int> order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
		return users.OrderBy(u => order[u.Id]).Select(u =>
		{
			int level = u.IsSuperAdmin ? UserLevels.SuperAdmin : u.Level;
			return new ManagedUserDto
			{
				Id = u.Id,
				Username = u.Username,
				Name = u.Name,
				Email = u.Email,
				Level = level,
				LevelName = UserLevels.Name(level),
				IsActive = u.IsActive,
				ParentUserId = u.ParentUserId,
				ParentName = u.ParentName,
				CreatedAt = u.CreatedAt,
				FingerprintCount = u.FingerprintCount,
				Grants = grants.Where(g => g.UserId == u.Id)
					.GroupBy(g => new { g.BookId, g.BookName, g.BusinessId, g.BusinessName })
					.Select(g => new BookGrantViewDto
					{
						BookId = g.Key.BookId,
						BookName = g.Key.BookName,
						BusinessId = g.Key.BusinessId,
						BusinessName = g.Key.BusinessName,
						Permissions = g.Select(x => x.Permission).OrderBy(x => x).ToList()
					}).OrderBy(g => g.BusinessName).ThenBy(g => g.BookName).ToList()
			};
		}).ToList();
	}

	/// <summary>
	/// يتحقق من الطلب ويعيده موحّداً. <paramref name="targetParentId"/> هو من أنشأ المستخدم
	/// المستهدف: إن كان أدمن، فلا تتجاوز صلاحيات المستهدف صلاحياته حتى لو منحها السوبر أدمن.
	/// </summary>
	private async Task<(Dictionary<Guid, HashSet<string>>? Grants, string? Error)> ValidateGrantsAsync(UserAccessInfo granter, int targetLevel, Guid? targetParentId, List<BookGrantDto>? requested)
	{
		Dictionary<Guid, HashSet<string>> result = new Dictionary<Guid, HashSet<string>>();
		UserAccessInfo? parent = targetParentId.HasValue ? await _permissions.GetUserAsync(targetParentId.Value) : null;
		bool parentConstrains = parent != null && !parent.IsSuperAdmin;
		foreach (BookGrantDto grant in requested ?? new List<BookGrantDto>())
		{
			if (grant == null || grant.BookId == Guid.Empty)
			{
				return (null, "معرّف خزنة غير صالح.");
			}
			if (!await _context.Books.AnyAsync(b => b.Id == grant.BookId))
			{
				return (null, "إحدى الخزائن المحددة غير موجودة.");
			}
			HashSet<string> codes = new HashSet<string>();
			foreach (string raw in grant.Permissions ?? new List<string>())
			{
				string? code = raw?.Trim().ToLowerInvariant();
				if (!Permissions.IsKnown(code))
				{
					return (null, "صلاحية غير معروفة: " + raw);
				}
				codes.Add(code);
			}
			if (codes.Count == 0)
			{
				continue;
			}
			// أي صلاحية على خزنة تستلزم رؤيتها.
			codes.Add(Permissions.BookView);
			if (codes.Contains(Permissions.UsersManage) && (targetLevel != UserLevels.Admin || !granter.IsSuperAdmin))
			{
				return (null, "صلاحية «إدارة المستخدمين» يمنحها السوبر أدمن للأدمن فقط.");
			}
			if (!granter.IsSuperAdmin)
			{
				HashSet<string> own = await _permissions.GetBookPermissionsAsync(granter.Id, grant.BookId);
				if (!own.Contains(Permissions.UsersManage))
				{
					return (null, "لا تملك صلاحية إدارة المستخدمين على إحدى الخزائن المحددة.");
				}
				string? missing = codes.FirstOrDefault(c => !own.Contains(c));
				if (missing != null)
				{
					return (null, "لا يمكنك منح صلاحية لا تملكها: " + PermissionName(missing));
				}
			}
			if (parentConstrains)
			{
				HashSet<string> parentOwn = await _permissions.GetBookPermissionsAsync(parent!.Id, grant.BookId);
				string? missing = codes.FirstOrDefault(c => !parentOwn.Contains(c));
				if (missing != null)
				{
					return (null, "صلاحيات المستخدم لا تتجاوز صلاحيات الأدمن الذي أنشأه: " + PermissionName(missing));
				}
			}
			if (result.TryGetValue(grant.BookId, out HashSet<string>? merged))
			{
				merged.UnionWith(codes);
			}
			else
			{
				result[grant.BookId] = codes;
			}
		}
		return (result, null);
	}

	private void ApplyBookGrant(List<UserWalletPermission> current, Guid userId, Guid bookId, HashSet<string> wanted, Guid grantedBy)
	{
		foreach (UserWalletPermission row in current.Where(p => !wanted.Contains(p.Permission)))
		{
			_context.UserWalletPermissions.Remove(row);
		}
		HashSet<string> have = current.Select(p => p.Permission).ToHashSet();
		foreach (string code in wanted.Where(c => !have.Contains(c)))
		{
			_context.UserWalletPermissions.Add(new UserWalletPermission
			{
				UserId = userId,
				BookId = bookId,
				Permission = code,
				GrantedByUserId = grantedBy
			});
		}
	}

	/// <summary>صلاحيات مستخدمي الأدمن تُقلَّص إلى تقاطعها مع صلاحياته الجديدة.</summary>
	private async Task CascadeToChildrenAsync(Guid adminId)
	{
		Dictionary<Guid, HashSet<string>> adminGrants = (await _context.UserWalletPermissions
				.Where(p => p.UserId == adminId).ToListAsync())
			.GroupBy(p => p.BookId)
			.ToDictionary(g => g.Key, g => g.Select(p => p.Permission).ToHashSet());
		List<Guid> childIds = await _context.Users.Where(u => u.ParentUserId == adminId).Select(u => u.Id).ToListAsync();
		if (childIds.Count == 0)
		{
			return;
		}
		List<UserWalletPermission> childRows = await _context.UserWalletPermissions.Where(p => childIds.Contains(p.UserId)).ToListAsync();
		foreach (UserWalletPermission row in childRows)
		{
			if (!adminGrants.TryGetValue(row.BookId, out HashSet<string>? allowed) || !allowed.Contains(row.Permission))
			{
				_context.UserWalletPermissions.Remove(row);
			}
		}
		await _context.SaveChangesAsync();
		foreach (Guid childId in childIds)
		{
			await SyncMembershipsAsync(childId, UserLevels.User);
		}
	}

	/// <summary>
	/// يُبقي جدول BusinessUsers (عضوية المنشأة) متوافقاً مع الصلاحيات: صف لكل منشأة فيها خزنة
	/// للمستخدم، ودوره = مستواه، وBookIds = خزائنه فيها. الصلاحيات هي المرجع، وهذا للتوافق.
	/// </summary>
	private async Task SyncMembershipsAsync(Guid userId, int level)
	{
		var books = await (from p in _context.UserWalletPermissions
			join b in _context.Books on p.BookId equals b.Id
			where p.UserId == userId
			select new { b.Id, b.BusinessId }).Distinct().ToListAsync();
		// يشمل ما أُضيف للتو ولم يُحفظ بعد.
		foreach (UserWalletPermission local in _context.UserWalletPermissions.Local.Where(p => p.UserId == userId))
		{
			Guid? bid = await _context.Books.Where(b => b.Id == local.BookId).Select(b => (Guid?)b.BusinessId).FirstOrDefaultAsync();
			if (bid.HasValue && !books.Any(x => x.Id == local.BookId))
			{
				books.Add(new { Id = local.BookId, BusinessId = bid.Value });
			}
		}
		List<BusinessUser> memberships = await _context.BusinessUsers.Where(bu => bu.UserId == userId).ToListAsync();
		string role = UserLevels.Name(level);
		foreach (IGrouping<Guid, Guid> group in books.GroupBy(b => b.BusinessId, b => b.Id))
		{
			BusinessUser? row = memberships.FirstOrDefault(m => m.BusinessId == group.Key);
			if (row == null)
			{
				row = new BusinessUser { UserId = userId, BusinessId = group.Key };
				_context.BusinessUsers.Add(row);
				memberships.Add(row);
			}
			row.Role = role;
			row.BookIds = group.Distinct().ToList();
		}
		foreach (BusinessUser stale in memberships.Where(m => !books.Any(b => b.BusinessId == m.BusinessId)))
		{
			stale.Role = role;
			stale.BookIds = new List<Guid>();
		}
	}

	private static string PermissionName(string code) =>
		Permissions.Catalog.FirstOrDefault(d => d.Code == code)?.Name ?? code;

	private static string BuildInternalEmail(string username)
	{
		string text = new string((username ?? "user").Trim().ToLowerInvariant()
			.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-').ToArray());
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "user";
		}
		return text + "@local.invalid";
	}
}
