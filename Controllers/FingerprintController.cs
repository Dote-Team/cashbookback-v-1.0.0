using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.fingerprint;
using cashbook.Services.Fingerprint;

namespace cashbook.Controllers;

/// <summary>
/// تسجيل البصمات وإدارتها. الدخول نفسه في <c>POST /API/User/login/fingerprint</c>.
/// يسجّل المستخدم بصمته بنفسه بعد دخوله بكلمة المرور، أو يسجّلها له من أنشأه.
/// </summary>
[Route("API/[Controller]")]
[Authorize]
public class FingerprintController : ApiControllerBase
{
	private readonly IFingerprintService _fingerprints;

	public FingerprintController(IFingerprintService fingerprints)
	{
		_fingerprints = fingerprints;
	}

	/// <summary>هل الدخول بالبصمة جاهز في الخادم؟ متاح بلا دخول لتعرض شاشة الدخول الزر أو تخفيه.</summary>
	[HttpGet("status")]
	[AllowAnonymous]
	public ActionResult<APIResponse> Status() => Success(_fingerprints.Status());

	[HttpPost("enroll")]
	public async Task<ActionResult<APIResponse>> Enroll([FromBody] EnrollFingerprintDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _fingerprints.EnrollAsync(callerId, dto ?? new EnrollFingerprintDto()));
	}

	[HttpGet("user/{userId:Guid}")]
	public async Task<ActionResult<APIResponse>> List(Guid userId)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _fingerprints.ListAsync(callerId, userId));
	}

	[HttpGet("me")]
	public async Task<ActionResult<APIResponse>> Mine()
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _fingerprints.ListAsync(callerId, callerId));
	}

	/// <summary>إلغاء بصمة (لا حذف).</summary>
	[HttpPost("{id:Guid}/revoke")]
	public async Task<ActionResult<APIResponse>> Revoke(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _fingerprints.RevokeAsync(callerId, id));
	}
}
