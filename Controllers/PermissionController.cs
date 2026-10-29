using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.userManagement;
using cashbook.Helper;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>قائمة الصلاحيات المعروفة، وصلاحيات المستخدم الحالي لإظهار الأزرار أو إخفائها في الواجهة.</summary>
[Route("API/[Controller]")]
[Authorize]
public class PermissionController : ApiControllerBase
{
	private readonly IPermissionService _permissions;

	public PermissionController(IPermissionService permissions)
	{
		_permissions = permissions;
	}

	/// <summary>قائمة الصلاحيات</summary>
	/// <remarks>
	/// كل الصلاحيات مع أسمائها العربية ومجموعاتها، لبناء شاشة منح الصلاحيات.
	///
	/// مع bookId تُعاد فقط الصلاحيات التي تستطيع **أنت** منحها على تلك الخزنة.
	/// users.manage (إدارة المستخدمين) يمنحها السوبر أدمن فقط.
	/// </remarks>
	/// <param name="bookId">اختياري: الخزنة التي تريد المنح عليها.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	[HttpGet("catalog")]
	public async Task<ActionResult<APIResponse>> Catalog([FromQuery] Guid? bookId = null)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		IEnumerable<Permissions.Definition> items = Permissions.Catalog;
		if (bookId.HasValue && !await _permissions.IsSuperAdminAsync(callerId))
		{
			HashSet<string> own = await _permissions.GetBookPermissionsAsync(callerId, bookId.Value);
			items = own.Contains(Permissions.UsersManage)
				? items.Where(d => own.Contains(d.Code) && !d.SuperAdminGrantOnly)
				: Enumerable.Empty<Permissions.Definition>();
		}
		return Success(items.Select(d => new PermissionDefinitionDto
		{
			Code = d.Code,
			Name = d.Name,
			Group = d.Group,
			SuperAdminGrantOnly = d.SuperAdminGrantOnly
		}).ToList());
	}

	/// <summary>صلاحياتي</summary>
	/// <remarks>
	/// صلاحيات المستخدم الحالي على كل خزنة، لإظهار الأزرار في الواجهة أو إخفائها.
	///
	/// - books: معرّف الخزنة ← رموز الصلاحيات.
	/// - business: كل الصلاحيات على خزائن المنشأة المطلوبة مجتمعة.
	/// - canManageUsers: هل يظهر له قسم المستخدمين.
	/// </remarks>
	/// <param name="businessId">اختياري: لحساب business لهذه المنشأة.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	[HttpGet("me")]
	public async Task<ActionResult<APIResponse>> Me([FromQuery] Guid? businessId = null)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		UserAccessInfo? user = await _permissions.GetUserAsync(callerId);
		if (user == null || !user.IsActive)
		{
			return NoCaller();
		}
		MyPermissionsDto result = new MyPermissionsDto
		{
			UserId = callerId,
			Level = user.Level,
			LevelName = UserLevels.Name(user.Level),
			IsSuperAdmin = user.IsSuperAdmin,
			CanManageUsers = user.IsSuperAdmin
		};
		if (businessId.HasValue)
		{
			HashSet<string> union = new HashSet<string>();
			foreach (Guid bookId in await _permissions.GetBookIdsAsync(callerId, businessId.Value))
			{
				HashSet<string> codes = await _permissions.GetBookPermissionsAsync(callerId, bookId);
				result.Books[bookId] = codes.OrderBy(c => c).ToList();
				union.UnionWith(codes);
			}
			result.Business = union.OrderBy(c => c).ToList();
			result.CanManageUsers |= union.Contains(Permissions.UsersManage);
		}
		else if (!user.IsSuperAdmin && user.Level == UserLevels.Admin)
		{
			foreach (Guid business in await _permissions.GetBusinessIdsAsync(callerId))
			{
				if (await _permissions.HasInBusinessAsync(callerId, business, Permissions.UsersManage))
				{
					result.CanManageUsers = true;
					break;
				}
			}
		}
		return Success(result);
	}
}
