using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>سلة المهملات القديمة (قبل نظام الأرشفة). للعرض فقط.</summary>
[Route("API/[Controller]")]
[Authorize]
public class TrashController : ApiControllerBase
{
	private readonly ITrashRepository _trashRepository;

	private readonly IPermissionService _permissions;

	public TrashController(ITrashRepository trashRepository, IPermissionService permissions)
	{
		_trashRepository = trashRepository;
		_permissions = permissions;
	}

	/// <summary>سلة المهملات القديمة</summary>
	/// <remarks>
	/// ما حُذف قبل تفعيل نظام الأرشفة. للعرض فقط. السوبر أدمن يرى الكل، وغيره يرى ما حذفه هو.
	/// </remarks>
	/// <param name="businessId">اختياري: منشأة واحدة.</param>
	/// <param name="skip">رقم الصفحة، يبدأ من 1.</param>
	/// <param name="take">عدد السجلات في الصفحة.</param>
	/// <param name="search">بحث.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	[HttpGet]
	public async Task<ActionResult<APIResponse>> GetTrash([FromQuery] Guid? businessId = null, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		// السوبر أدمن يرى كل شيء، وغيره يرى ما حذفه هو فقط كما كان سابقاً.
		Guid? deletedBy = await _permissions.IsSuperAdminAsync(callerId) ? null : callerId;
		return Success(await _trashRepository.GetTrashAsync(businessId, deletedBy, Page(skip), PageSize(take), search));
	}

	/// <summary>متوقف: لا استرجاع في النظام</summary>
	/// <remarks>
	/// يعيد 410 دائماً. الأرشفة دون رجعة.
	/// </remarks>
	/// <param name="id">غير مستخدم.</param>
	/// <response code="410">المسار متوقف نهائياً.</response>
	[HttpPost("{id:Guid}/restore")]
	public ActionResult<APIResponse> Restore(Guid id) =>
		Fail(HttpStatusCode.Gone, "الاسترجاع غير متاح: النظام يعتمد الأرشفة دون رجعة.");

	/// <summary>متوقف: لا حذف نهائي في النظام</summary>
	/// <remarks>
	/// يعيد 410 دائماً.
	/// </remarks>
	/// <param name="id">غير مستخدم.</param>
	/// <response code="410">المسار متوقف نهائياً.</response>
	[HttpDelete("{id:Guid}")]
	public ActionResult<APIResponse> HardDelete(Guid id) =>
		Fail(HttpStatusCode.Gone, "الحذف النهائي غير متاح في النظام.");
}
