using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Helper;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>
/// أساس مشترك للمتحكمات الجديدة والمعاد كتابتها: بنية الاستجابة (APIResponse) نفسها التي
/// تعتمدها الواجهة، مع رموز HTTP صحيحة. الأخطاء غير المتوقعة يلتقطها معالج الأخطاء العام.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
	protected Guid? CallerId => User.GetUserId();

	protected ActionResult<APIResponse> Success(object? result = null, HttpStatusCode status = HttpStatusCode.OK)
	{
		return StatusCode((int)status, new APIResponse
		{
			StatusCode = status,
			IsSuccess = true,
			Result = result
		});
	}

	protected ActionResult<APIResponse> Fail(HttpStatusCode status, string message)
	{
		return StatusCode((int)status, new APIResponse
		{
			StatusCode = status,
			IsSuccess = false,
			ErrorMessages = new List<string> { message }
		});
	}

	protected ActionResult<APIResponse> NoCaller() => Fail(HttpStatusCode.Unauthorized, "رمز الدخول غير صالح.");

	protected ActionResult<APIResponse> Denied(AccessStatus status, string forbiddenMessage)
	{
		return status == AccessStatus.NotFound
			? Fail(HttpStatusCode.NotFound, "الخزنة غير موجودة.")
			: Fail(HttpStatusCode.Forbidden, forbiddenMessage);
	}

	protected ActionResult<APIResponse> FromResult(ServiceResult result)
	{
		return result.Ok ? Success(result.Result) : Fail(result.Status, result.Error ?? "تعذّر تنفيذ العملية.");
	}

	protected static int Page(int? skip) => Math.Max(1, skip ?? 1);

	/// <summary>حجم الصفحة محدود دائماً، فلا يطلب أحد مليون صف في طلب واحد.</summary>
	protected static int PageSize(int? take, int max = 200) => Math.Clamp(take ?? 25, 1, max);
}
