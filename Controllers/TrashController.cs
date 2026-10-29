using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>
/// سلة المهملات القديمة: ما حُذف قبل تفعيل نظام الأرشفة. للعرض فقط، فالنظام لا يسترجع
/// ولا يحذف نهائياً. المحذوفات الجديدة لا تصل إلى هنا، بل تُؤرشف (انظر ArchiveController).
/// </summary>
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

	[HttpPost("{id:Guid}/restore")]
	public ActionResult<APIResponse> Restore(Guid id) =>
		Fail(HttpStatusCode.Gone, "الاسترجاع غير متاح: النظام يعتمد الأرشفة دون رجعة.");

	[HttpDelete("{id:Guid}")]
	public ActionResult<APIResponse> HardDelete(Guid id) =>
		Fail(HttpStatusCode.Gone, "الحذف النهائي غير متاح في النظام.");
}
