using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.customfield;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>الحقول المخصصة لكل خزنة (مثل رقم الفاتورة). قيمها تُرسل مع القيد في CustomFieldValues.</summary>
[Route("API/[Controller]")]
[Authorize]
public class CustomFieldController : ApiControllerBase
{
	private readonly ICustomFieldRepository _customFieldRepository;

	private readonly IPermissionService _permissions;

	public CustomFieldController(ICustomFieldRepository customFieldRepository, IPermissionService permissions)
	{
		_customFieldRepository = customFieldRepository;
		_permissions = permissions;
	}

	/// <summary>حقول خزنة</summary>
	/// <remarks>
	/// **الصلاحية:** book.view على الخزنة.
	/// </remarks>
	/// <param name="bookId">معرّف الخزنة.</param>
	/// <param name="skip">رقم الصفحة، يبدأ من 1.</param>
	/// <param name="take">عدد السجلات في الصفحة.</param>
	/// <param name="search">بحث في اسم الحقل.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">الخزنة غير موجودة.</response>
	[HttpGet]
	public async Task<ActionResult<APIResponse>> GetPaginatedCustomField([FromQuery] Guid bookId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		// من يقرأ حركات الخزنة يقرأ حقولها: قيم الحقول تُعاد داخل الحركة أصلاً.
		BookAccess access = await _permissions.CheckBookAsync(callerId, bookId, Permissions.BookView);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية الوصول إلى حقول هذه الخزنة.");
		}
		return Success(await _customFieldRepository.GetcustomFieldAsync(bookId, Page(skip), PageSize(take), search));
	}

	/// <summary>حقل واحد</summary>
	/// <remarks>
	/// **الصلاحية:** book.view على خزنة الحقل.
	/// </remarks>
	/// <param name="id">معرّف الحقل.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpGet("{id:Guid}", Name = "GetCustomField")]
	public async Task<ActionResult<APIResponse>> GetCustomField(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		CustomField? customField = await _customFieldRepository.GetAsync(f => f.Id == id && !f.IsArchived, tracked: false);
		if (customField == null)
		{
			return Fail(HttpStatusCode.NotFound, "الحقل غير موجود.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, customField.BookId, Permissions.BookView);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية الوصول إلى هذا الحقل.");
		}
		return Success(customField);
	}

	/// <summary>إضافة حقل مخصص</summary>
	/// <remarks>
	/// **الصلاحية:** custom_field.manage على الخزنة.
	///
	/// isRequired = true يجعل الحقل إلزامياً عند إضافة قيد.
	/// </remarks>
	/// <param name="createCustomFieldDto">اسم الحقل والخزنة وهل هو إلزامي.</param>
	/// <response code="201">أُضيف الحقل.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">الخزنة غير موجودة.</response>
	[HttpPost]
	public async Task<ActionResult<APIResponse>> CreateCustomField([FromBody] CreateCustomFieldDto createCustomFieldDto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (createCustomFieldDto == null || string.IsNullOrWhiteSpace(createCustomFieldDto.Key))
		{
			return Fail(HttpStatusCode.BadRequest, "اسم الحقل مطلوب.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, createCustomFieldDto.BookId, Permissions.CustomFieldManage);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية إضافة حقل في هذه الخزنة.");
		}
		CustomField customField = new CustomField
		{
			Key = createCustomFieldDto.Key.Trim(),
			BookId = createCustomFieldDto.BookId,
			IsRequired = createCustomFieldDto.IsRequired
		};
		await _customFieldRepository.CreateAsync(customField);
		return Success(customField, HttpStatusCode.Created);
	}

	/// <summary>أرشفة حقل (لا حذف)</summary>
	/// <remarks>
	/// **الصلاحية:** custom_field.manage على الخزنة. قيمه في القيود القديمة تبقى.
	/// </remarks>
	/// <param name="id">معرّف الحقل.</param>
	/// <param name="reason">سبب الأرشفة (اختياري).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpDelete("{id:Guid}", Name = "DeleteCustomField")]
	public async Task<ActionResult<APIResponse>> DeleteCustomField(Guid id, [FromQuery] string? reason = null)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		CustomField? customField = await _customFieldRepository.GetAsync(f => f.Id == id && !f.IsArchived);
		if (customField == null)
		{
			return Fail(HttpStatusCode.NotFound, "الحقل غير موجود.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, customField.BookId, Permissions.CustomFieldManage);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية أرشفة حقل من هذه الخزنة.");
		}
		await _customFieldRepository.ArchiveAsync(customField, callerId, reason);
		return Success(new { archived = true });
	}

	/// <summary>تعديل حقل</summary>
	/// <remarks>
	/// **الصلاحية:** custom_field.manage على الخزنة. أرسل ما تريد تغييره فقط (key أو isRequired).
	/// </remarks>
	/// <param name="id">معرّف الحقل.</param>
	/// <param name="updateCustomFieldDto">الحقول المراد تغييرها.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPut("{id:Guid}", Name = "UpdateCustomField")]
	public async Task<ActionResult<APIResponse>> UpdateCustomField([FromRoute] Guid id, [FromBody] UpdateCustomFieldDto updateCustomFieldDto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (updateCustomFieldDto == null)
		{
			return Fail(HttpStatusCode.BadRequest, "بيانات غير صالحة.");
		}
		CustomField? existingCustomField = await _customFieldRepository.GetAsync(f => f.Id == id && !f.IsArchived);
		if (existingCustomField == null)
		{
			return Fail(HttpStatusCode.NotFound, "الحقل غير موجود.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, existingCustomField.BookId, Permissions.CustomFieldManage);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية تعديل حقل في هذه الخزنة.");
		}
		if (!string.IsNullOrWhiteSpace(updateCustomFieldDto.Key))
		{
			existingCustomField.Key = updateCustomFieldDto.Key.Trim();
		}
		if (updateCustomFieldDto.IsRequired.HasValue)
		{
			existingCustomField.IsRequired = updateCustomFieldDto.IsRequired.Value;
		}
		await _customFieldRepository.UpdateAsync(existingCustomField);
		return Success(existingCustomField);
	}
}
