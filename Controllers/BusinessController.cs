using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.business;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>المنشآت (الحافظات الرئيسية). الإنشاء والتعديل والأرشفة للسوبر أدمن فقط، والعرض لكل عضو.</summary>
[Route("API/[Controller]")]
[Authorize]
public class BusinessController : ApiControllerBase
{
	private readonly IBusinessRepository _businessRepository;

	private readonly IPermissionService _permissions;

	public BusinessController(IBusinessRepository businessRepository, IPermissionService permissions)
	{
		_businessRepository = businessRepository;
		_permissions = permissions;
	}

	/// <summary>المنشآت التي أنا عضو فيها</summary>
	/// <remarks>
	/// السوبر أدمن يرى كل المنشآت. غيره يرى المنشآت التي له صلاحية على خزنة فيها، ومع كل منشأة خزائنه فقط.
	/// </remarks>
	/// <param name="skip">رقم الصفحة، يبدأ من 1.</param>
	/// <param name="take">عدد السجلات في الصفحة.</param>
	/// <param name="search">بحث في اسم المنشأة.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	[HttpGet]
	public async Task<ActionResult<APIResponse>> GetPaginatedBusinesses([FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		UserAccessInfo? caller = await _permissions.GetUserAsync(callerId);
		if (caller == null || !caller.IsActive)
		{
			return NoCaller();
		}
		List<Guid> businessIds = await _permissions.GetBusinessIdsAsync(callerId);
		List<Guid>? books = caller.IsSuperAdmin ? null : await AccessibleBooksAsync(callerId, businessIds);
		return Success(await _businessRepository.GetPaginatedBusinessesAsync(businessIds, books, UserLevels.Name(caller.Level), skip, take, search));
	}

	/// <summary>بيانات منشأة</summary>
	/// <remarks>
	/// **الصلاحية:** عضو في المنشأة. الخزائن المعادة هي التي تملك book.view عليها.
	/// </remarks>
	/// <param name="id">معرّف المنشأة.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpGet("{id:Guid}", Name = "GetBusiness")]
	public async Task<ActionResult<APIResponse>> GetBusiness(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (!await _permissions.IsBusinessMemberAsync(callerId, id))
		{
			return Fail(HttpStatusCode.NotFound, "المنشأة غير موجودة.");
		}
		List<Guid>? books = await _permissions.IsSuperAdminAsync(callerId)
			? null
			: await _permissions.GetBookIdsAsync(callerId, id, Permissions.BookView);
		BusinessWithBooksDto? business = await _businessRepository.GetBusinessWithBooksDtoByIdAsync(id, books);
		return business == null ? Fail(HttpStatusCode.NotFound, "المنشأة غير موجودة.") : Success(business);
	}

	/// <summary>إنشاء منشأة</summary>
	/// <remarks>
	/// **الصلاحية:** السوبر أدمن فقط.
	///
	/// بعدها أنشئ خزائنها (POST /API/Book) ثم أدمن لها (POST /API/UserManagement).
	/// </remarks>
	/// <param name="createBusinessDto">اسم المنشأة.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	[HttpPost]
	public async Task<ActionResult<APIResponse>> CreateBusiness([FromBody] CreateBusinessDto createBusinessDto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (!await _permissions.IsSuperAdminAsync(callerId))
		{
			return Fail(HttpStatusCode.Forbidden, "إنشاء المنشآت للسوبر أدمن فقط.");
		}
		if (createBusinessDto == null || string.IsNullOrWhiteSpace(createBusinessDto.Name))
		{
			return Fail(HttpStatusCode.BadRequest, "اسم المنشأة مطلوب.");
		}
		Guid id = await _businessRepository.CreateBussinessAsync(createBusinessDto);
		return Success(new { Id = id }, HttpStatusCode.Created);
	}

	/// <summary>أرشفة منشأة (لا حذف)</summary>
	/// <remarks>
	/// **الصلاحية:** السوبر أدمن فقط.
	///
	/// المنشأة وكل خزائنها تختفي من القوائم دون رجعة، وتبقى في الأرشيف. مثل POST /{id}/archive.
	/// </remarks>
	/// <param name="id">معرّف المنشأة.</param>
	/// <param name="reason">سبب الأرشفة (اختياري، يظهر في الأرشيف).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpDelete("{id:Guid}", Name = "DeleteBusiness")]
	public Task<ActionResult<APIResponse>> DeleteBusiness(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

	/// <summary>أرشفة منشأة</summary>
	/// <remarks>
	/// **الصلاحية:** السوبر أدمن فقط. دون رجعة.
	/// </remarks>
	/// <param name="id">معرّف المنشأة.</param>
	/// <param name="reason">سبب الأرشفة (اختياري).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPost("{id:Guid}/archive")]
	public Task<ActionResult<APIResponse>> ArchiveBusiness(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

	private async Task<ActionResult<APIResponse>> ArchiveCore(Guid id, string? reason)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (!await _permissions.IsSuperAdminAsync(callerId))
		{
			return Fail(HttpStatusCode.Forbidden, "أرشفة المنشآت للسوبر أدمن فقط.");
		}
		if (!await _businessRepository.ArchiveBusinessAsync(id, callerId, reason))
		{
			return Fail(HttpStatusCode.NotFound, "المنشأة غير موجودة.");
		}
		return Success(new { archived = true });
	}

	/// <summary>تعديل اسم منشأة</summary>
	/// <remarks>
	/// **الصلاحية:** السوبر أدمن فقط.
	/// </remarks>
	/// <param name="id">معرّف المنشأة.</param>
	/// <param name="updateBusinessDto">الاسم الجديد.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPut("{id:Guid}", Name = "UpdateBusiness")]
	public async Task<ActionResult<APIResponse>> UpdateBusiness([FromRoute] Guid id, [FromBody] UpdateBusinessDto updateBusinessDto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (!await _permissions.IsSuperAdminAsync(callerId))
		{
			return Fail(HttpStatusCode.Forbidden, "تعديل المنشآت للسوبر أدمن فقط.");
		}
		Business? business = await _businessRepository.GetAsync(b => b.Id == id);
		if (business == null)
		{
			return Fail(HttpStatusCode.NotFound, "المنشأة غير موجودة.");
		}
		if (!string.IsNullOrWhiteSpace(updateBusinessDto?.Name))
		{
			business.Name = updateBusinessDto.Name.Trim();
		}
		business.UpdatedAt = DateTime.UtcNow;
		await _businessRepository.UpdateAsync(business);
		return Success(new BusinessDto
		{
			Id = business.Id,
			Name = business.Name,
			CreatedAt = business.CreatedAt,
			UpdatedAt = business.UpdatedAt
		});
	}

	private async Task<List<Guid>> AccessibleBooksAsync(Guid userId, List<Guid> businessIds)
	{
		List<Guid> books = new List<Guid>();
		foreach (Guid businessId in businessIds)
		{
			books.AddRange(await _permissions.GetBookIdsAsync(userId, businessId, Permissions.BookView));
		}
		return books;
	}
}
