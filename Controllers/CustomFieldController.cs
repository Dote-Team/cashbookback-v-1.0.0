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

/// <summary>
/// الحقول المخصصة لكل خزنة. الصلاحية تُفحص على خزنة الحقل نفسه، والأخطاء غير المتوقعة
/// يلتقطها المعالج العام فلا يصل نص الاستثناء إلى العميل.
/// </summary>
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

	/// <summary>لا حذف في النظام: هذا المسار يؤرشف الحقل. القيم المسجلة في القيود القديمة تبقى.</summary>
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
