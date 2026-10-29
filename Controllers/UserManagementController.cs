using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.userManagement;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>
/// إدارة المستخدمين بالوراثة: السوبر أدمن ينشئ أدمن، والأدمن ينشئ مستخدمين بصلاحيات من ضمن
/// صلاحياته. القواعد كلها في <see cref="UserManagementService"/>.
/// </summary>
[Route("API/[Controller]")]
[Authorize]
public class UserManagementController : ApiControllerBase
{
	private readonly IUserManagementService _users;

	public UserManagementController(IUserManagementService users)
	{
		_users = users;
	}

	[HttpGet]
	public async Task<ActionResult<APIResponse>> List([FromQuery] Guid? businessId = null, [FromQuery] string? search = null, [FromQuery] bool includeInactive = true, [FromQuery] int? skip = 1, [FromQuery] int? take = 25)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return Success(await _users.ListAsync(callerId, businessId, search, includeInactive, Page(skip), PageSize(take)));
	}

	[HttpGet("{id:Guid}")]
	public async Task<ActionResult<APIResponse>> Get(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		ManagedUserDto? user = await _users.GetAsync(callerId, id);
		return user == null ? Fail(HttpStatusCode.NotFound, "المستخدم غير موجود.") : Success(user);
	}

	[HttpPost]
	public async Task<ActionResult<APIResponse>> Create([FromBody] CreateManagedUserDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (dto == null)
		{
			return Fail(HttpStatusCode.BadRequest, "بيانات غير صالحة.");
		}
		ServiceResult result = await _users.CreateUserAsync(callerId, dto);
		return result.Ok ? Success(result.Result, HttpStatusCode.Created) : FromResult(result);
	}

	[HttpPut("{id:Guid}")]
	public async Task<ActionResult<APIResponse>> Update(Guid id, [FromBody] UpdateManagedUserDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.UpdateUserAsync(callerId, id, dto ?? new UpdateManagedUserDto()));
	}

	/// <summary>استبدال صلاحيات المستخدم. سحب صلاحية من أدمن يسحبها من مستخدميه تلقائياً.</summary>
	[HttpPut("{id:Guid}/permissions")]
	public async Task<ActionResult<APIResponse>> SetPermissions(Guid id, [FromBody] UpdateGrantsDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.SetGrantsAsync(callerId, id, dto?.Grants ?? new()));
	}

	/// <summary>تعطيل المستخدم (لا حذف). تعطيل الأدمن يعطّل كل من أنشأهم.</summary>
	[HttpPost("{id:Guid}/deactivate")]
	public async Task<ActionResult<APIResponse>> Deactivate(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.DeactivateAsync(callerId, id));
	}

	[HttpPost("{id:Guid}/activate")]
	public async Task<ActionResult<APIResponse>> Activate(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.ActivateAsync(callerId, id));
	}

	[HttpPost("{id:Guid}/reset-password")]
	public async Task<ActionResult<APIResponse>> ResetPassword(Guid id, [FromBody] ResetPasswordDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.ResetPasswordAsync(callerId, id, dto?.Password ?? ""));
	}
}
