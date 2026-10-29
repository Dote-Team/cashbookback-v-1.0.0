using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.fingerprint;
using cashbook.Services.Fingerprint;

namespace cashbook.Controllers;

/// <summary>تسجيل بصمات المستخدمين وإلغاؤها. الصورة تأتي من البرنامج الوسيط على جهاز المستخدم (127.0.0.1:52181)، والمطابقة في الخادم.</summary>
[Route("API/[Controller]")]
[Authorize]
public class FingerprintController : ApiControllerBase
{
	private readonly IFingerprintService _fingerprints;

	public FingerprintController(IFingerprintService fingerprints)
	{
		_fingerprints = fingerprints;
	}

	/// <summary>هل الدخول بالبصمة متاح؟</summary>
	/// <remarks>
	/// **لا يحتاج دخولاً.** تستخدمه شاشة الدخول لإظهار زر البصمة أو إخفائه.
	///
	/// available = false مع reason يعني أن الخادم غير مضبوط (مثلاً Fingerprint__EncryptionKey ناقص).
	/// </remarks>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	[HttpGet("status")]
	[AllowAnonymous]
	public ActionResult<APIResponse> Status() => Success(_fingerprints.Status());

	/// <summary>تسجيل بصمة</summary>
	/// <remarks>
	/// **الصلاحية:** المستخدم لنفسه، أو من أنشأه، أو السوبر أدمن.
	///
	/// الخطوات:
	/// 1. التقط الإصبع نفسه 3 مرات من البرنامج الوسيط (POST http://127.0.0.1:52181/capture).
	/// 2. أرسل الصور الثلاث في samples، ورقم الإصبع في fingerIndex.
	///
	/// تُرفض العينات إن لم تتطابق مع بعضها. الحد 3 أصابع لكل مستخدم. تسجيل إصبع مسجّل يستبدله.
	/// </remarks>
	/// <param name="dto">المستخدم (فارغ = أنت)، ورقم الإصبع، والعينات.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">العينات ناقصة أو غير متطابقة أو بلا تفاصيل كافية.</response>
	/// <response code="503">البصمة غير مفعّلة في الخادم.</response>
	[HttpPost("enroll")]
	public async Task<ActionResult<APIResponse>> Enroll([FromBody] EnrollFingerprintDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _fingerprints.EnrollAsync(callerId, dto ?? new EnrollFingerprintDto()));
	}

	/// <summary>بصمات مستخدم</summary>
	/// <remarks>
	/// **الصلاحية:** من أنشأه، أو السوبر أدمن. تُعاد معلومات البصمات فقط (الإصبع والتاريخ)، لا القوالب.
	/// </remarks>
	/// <param name="userId">معرّف المستخدم.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	[HttpGet("user/{userId:Guid}")]
	public async Task<ActionResult<APIResponse>> List(Guid userId)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _fingerprints.ListAsync(callerId, userId));
	}

	/// <summary>بصماتي</summary>
	/// <remarks>
	/// البصمات المسجّلة للمستخدم الحالي.
	/// </remarks>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	[HttpGet("me")]
	public async Task<ActionResult<APIResponse>> Mine()
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _fingerprints.ListAsync(callerId, callerId));
	}

	/// <summary>إلغاء بصمة</summary>
	/// <remarks>
	/// **الصلاحية:** صاحبها، أو من أنشأه، أو السوبر أدمن.
	///
	/// البصمة الملغاة لا تُقبل للدخول. لا تُحذف من قاعدة البيانات.
	/// </remarks>
	/// <param name="id">معرّف البصمة (من /me أو /user/{userId}).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
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
