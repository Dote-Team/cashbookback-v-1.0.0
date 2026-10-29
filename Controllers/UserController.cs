using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using cashbook.Dto;
using cashbook.Dto.fingerprint;
using cashbook.Dto.user;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;
using cashbook.Services.Fingerprint;

namespace cashbook.Controllers;

/// <summary>الدخول والخروج وتجديد الرمز، والملف الشخصي. ابدأ من هنا: POST /API/User/login يعطيك accessToken تضعه في زر Authorize.</summary>
[Route("API/[Controller]")]
public class UserController : ApiControllerBase
{
	private readonly IUserRepository _userRepo;

	private readonly ILoginThrottle _loginThrottle;

	private readonly IPermissionService _permissions;

	private readonly IUserManagementService _users;

	private readonly IFingerprintService _fingerprints;

	private readonly ITokenService _tokens;

	private readonly IFileStorage _files;

	public UserController(IUserRepository userRepo, ILoginThrottle loginThrottle, IPermissionService permissions, IUserManagementService users, IFingerprintService fingerprints, ITokenService tokens, IFileStorage files)
	{
		_files = files;
		_userRepo = userRepo;
		_loginThrottle = loginThrottle;
		_permissions = permissions;
		_users = users;
		_fingerprints = fingerprints;
		_tokens = tokens;
	}

	private string ThrottleKey(string? username)
	{
		string ip = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
		return (username ?? string.Empty).Trim().ToLowerInvariant() + "|" + ip;
	}

	/// <summary>متوقف: التسجيل الذاتي مغلق</summary>
	/// <remarks>
	/// الحسابات تُنشأ من داخل النظام فقط عبر POST /API/UserManagement. هذا المسار يعيد 410 دائماً.
	/// </remarks>
	/// <response code="410">المسار متوقف نهائياً.</response>
	[HttpPost("register")]
	public ActionResult<APIResponse> Register() =>
		Fail(HttpStatusCode.Gone, "إنشاء الحسابات من داخل النظام فقط. تواصل مع مسؤول النظام.");

	/// <summary>تسجيل الدخول باسم المستخدم وكلمة المرور</summary>
	/// <remarks>
	/// **لا يحتاج دخولاً.**
	///
	/// أرسل username (أو email) و password. deviceToken اختياري لتمييز الجهاز في سجل التدقيق.
	///
	/// النتيجة فيها:
	/// - accessToken: ضعه في زر Authorize أعلى الصفحة. صالح 60 دقيقة.
	/// - refreshToken: لتجديد الرمز عبر refresh-token دون إعادة الدخول.
	/// - level و levelName: 1 سوبر أدمن، 2 أدمن، 3 مستخدم.
	///
	/// الحماية: 5 محاولات خاطئة لنفس الاسم من نفس العنوان تقفل الدخول 15 دقيقة، و10 طلبات في الدقيقة لكل عنوان.
	///
	/// مثال:
	///
	///     { "username": "admin", "password": "..." }
	/// </remarks>
	/// <param name="loginRequestDto">اسم المستخدم أو البريد، وكلمة المرور.</param>
	/// <response code="200">تم الدخول. الرموز في result.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="401">اسم المستخدم أو كلمة المرور غير صحيحة.</response>
	/// <response code="429">محاولات كثيرة. انتظر ثم أعد المحاولة.</response>
	[HttpPost("login")]
	[EnableRateLimiting(RateLimitPolicies.Login)]
	public async Task<ActionResult<APIResponse>> Login([FromBody] LoginRequestDto loginRequestDto)
	{
		if (loginRequestDto == null)
		{
			return Fail(HttpStatusCode.BadRequest, "بيانات غير صالحة.");
		}
		string key = ThrottleKey(loginRequestDto.Username ?? loginRequestDto.Email);
		if (IsThrottled(key) is { } blocked)
		{
			return blocked;
		}
		LoginResponseDto? login = await _userRepo.Login(loginRequestDto);
		if (login == null)
		{
			_loginThrottle.RecordFailure(key);
			return Fail(HttpStatusCode.Unauthorized, "اسم المستخدم أو كلمة المرور غير صحيحة.");
		}
		_loginThrottle.Reset(key);
		return Success(login);
	}

	/// <summary>تسجيل الدخول بالبصمة</summary>
	/// <remarks>
	/// **لا يحتاج دخولاً.**
	///
	/// الخطوات:
	/// 1. التقط البصمة من البرنامج الوسيط: POST http://127.0.0.1:52181/capture
	/// 2. أرسل نتيجته هنا كما هي في sample (width و height و dpi و pixels).
	///
	/// username اختياري:
	/// - معه: تُطابق البصمة مع بصمات هذا المستخدم فقط.
	/// - بدونه: يُبحث عن صاحب البصمة بين كل المستخدمين. إن تطابقت مع أكثر من مستخدم يُرفض الدخول.
	///
	/// النتيجة مثل login تماماً.
	/// </remarks>
	/// <param name="dto">صورة البصمة، واسم المستخدم اختيارياً.</param>
	/// <response code="200">تم الدخول.</response>
	/// <response code="400">صورة البصمة مفقودة أو غير صالحة.</response>
	/// <response code="401">البصمة غير مطابقة.</response>
	/// <response code="429">محاولات كثيرة.</response>
	/// <response code="503">الدخول بالبصمة غير مفعّل في الخادم (راجع Fingerprint__EncryptionKey).</response>
	[HttpPost("login/fingerprint")]
	[EnableRateLimiting(RateLimitPolicies.Login)]
	public async Task<ActionResult<APIResponse>> LoginWithFingerprint([FromBody] FingerprintLoginDto dto)
	{
		if (dto == null || dto.Sample == null)
		{
			return Fail(HttpStatusCode.BadRequest, "صورة البصمة مطلوبة.");
		}
		if (!_fingerprints.Status().Available)
		{
			return Fail(HttpStatusCode.ServiceUnavailable, _fingerprints.Status().Reason ?? "الدخول بالبصمة غير متاح.");
		}
		string key = ThrottleKey(dto.Username);
		if (IsThrottled(key) is { } blocked)
		{
			return blocked;
		}
		User? user = await _fingerprints.VerifyAsync(dto.Username, dto.Sample);
		if (user == null)
		{
			_loginThrottle.RecordFailure(key);
			return Fail(HttpStatusCode.Unauthorized, "البصمة غير مطابقة.");
		}
		_loginThrottle.Reset(key);
		return Success(await _tokens.IssueAsync(user, dto.DeviceToken, "fp"));
	}

	/// <summary>قائمة أعضاء منشأة</summary>
	/// <remarks>
	/// **الصلاحية:** عضو في المنشأة.
	///
	/// لإدارة المستخدمين وصلاحياتهم استخدم /API/UserManagement.
	/// </remarks>
	/// <param name="businessId">معرّف المنشأة.</param>
	/// <param name="skip">رقم الصفحة، يبدأ من 1.</param>
	/// <param name="take">عدد السجلات في الصفحة (حد أقصى 200).</param>
	/// <param name="search">بحث في الاسم أو اسم المستخدم.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	[Authorize]
	[HttpGet]
	public async Task<ActionResult<APIResponse>> GetPaginatedUsers([FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (!await _permissions.IsBusinessMemberAsync(callerId, businessId))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية على هذه المنشأة.");
		}
		return Success(await _userRepo.GetAllUsersAsync(businessId, skip, take, search));
	}

	/// <summary>بيانات عضو في منشأة</summary>
	/// <remarks>
	/// **الصلاحية:** عضو في المنشأة نفسها.
	/// </remarks>
	/// <param name="id">معرّف المستخدم.</param>
	/// <param name="businessId">المنشأة التي تُعرض عضويته فيها.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[Authorize]
	[HttpGet("{id}", Name = "GetUserById")]
	public async Task<ActionResult<APIResponse>> GetUserById(Guid id, Guid businessId)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (!await _permissions.IsBusinessMemberAsync(callerId, businessId))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية على هذه المنشأة.");
		}
		UserDto? user = await _userRepo.GetUserByIdAsync(id, businessId);
		return user == null ? Fail(HttpStatusCode.NotFound, "المستخدم غير موجود.") : Success(user);
	}

	/// <summary>تعطيل مستخدم (لا حذف)</summary>
	/// <remarks>
	/// **الصلاحية:** من أنشأه، أو السوبر أدمن.
	///
	/// يُعطَّل الحساب وتُنهى كل جلساته فوراً. تعطيل أدمن يعطّل معه كل المستخدمين الذين أنشأهم.
	/// مثل POST /API/UserManagement/{id}/deactivate.
	/// </remarks>
	/// <param name="id">معرّف المستخدم.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[Authorize]
	[HttpDelete("{id}")]
	public async Task<ActionResult<APIResponse>> DeleteUser(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.DeactivateAsync(callerId, id));
	}

	/// <summary>تعديل الملف الشخصي</summary>
	/// <remarks>
	/// **الصلاحية:** المستخدم نفسه، أو من أنشأه. تغيير اسم الدخول لشخص آخر للسوبر أدمن فقط.
	///
	/// يُرسل كـ multipart/form-data. أرسل الحقول التي تريد تغييرها فقط.
	/// - ProfileImage: صورة JPG أو PNG أو GIF أو WEBP، حتى 10MB.
	/// - Password: 8 أحرف على الأقل، فيها حرف ورقم.
	/// </remarks>
	/// <param name="id">معرّف المستخدم.</param>
	/// <param name="userUpdateDto">الحقول المراد تغييرها.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="409">اسم الدخول مستخدم مسبقاً.</response>
	[Authorize]
	[HttpPatch("{id}")]
	public async Task<ActionResult<APIResponse>> UpdateUser([FromRoute] Guid id, [FromForm] UserUpdateDto userUpdateDto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		bool isSelf = callerId == id;
		if (!isSelf && !await _users.CanManageAsync(callerId, id))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية تعديل هذا المستخدم.");
		}
		if (!string.IsNullOrWhiteSpace(userUpdateDto.Password) && !PasswordPolicy.IsValid(userUpdateDto.Password, out string passwordError))
		{
			return Fail(HttpStatusCode.BadRequest, passwordError);
		}
		if (userUpdateDto.ProfileImage != null && _files.Validate(new[] { userUpdateDto.ProfileImage }, UploadKind.ProfileImage) is { } fileError)
		{
			return Fail(HttpStatusCode.BadRequest, fileError);
		}
		if (!isSelf && !string.IsNullOrWhiteSpace(userUpdateDto.Username) && !await _permissions.IsSuperAdminAsync(callerId))
		{
			return Fail(HttpStatusCode.Forbidden, "تغيير اسم دخول مستخدم آخر للسوبر أدمن فقط.");
		}
		if (!await _userRepo.UpdateUserAsync(id, userUpdateDto))
		{
			return Fail(HttpStatusCode.Conflict, "المستخدم غير موجود أو اسم الدخول مستخدم مسبقاً.");
		}
		return Success();
	}

	/// <summary>تجديد رمز الدخول</summary>
	/// <remarks>
	/// **لا يحتاج دخولاً.**
	///
	/// أرسل refreshToken الذي حصلت عليه عند الدخول، فتحصل على accessToken و refreshToken جديدين.
	/// رمز التحديث القديم يبطل فور استخدامه، فاحفظ الجديد دائماً. صالح 20 يوماً.
	///
	/// **ملاحظة:** هذا المسار يعيد الرموز مباشرة، لا داخل result.
	/// </remarks>
	/// <param name="refreshTokenRequest">رمز التحديث.</param>
	/// <response code="200">رموز جديدة.</response>
	/// <response code="400">رمز التحديث مفقود.</response>
	/// <response code="401">رمز التحديث غير صالح أو منتهٍ أو مستخدم مسبقاً.</response>
	/// <response code="429">طلبات كثيرة.</response>
	[HttpPost("refresh-token")]
	[EnableRateLimiting(RateLimitPolicies.Refresh)]
	public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto refreshTokenRequest)
	{
		if (refreshTokenRequest == null || string.IsNullOrEmpty(refreshTokenRequest.RefreshToken))
		{
			return BadRequest("Invalid client request");
		}
		LoginResponseDto? response = await _userRepo.RefreshTokenAsync(refreshTokenRequest.RefreshToken);
		if (response == null)
		{
			return Unauthorized("Invalid or expired refresh token");
		}
		return Ok(response);
	}

	/// <summary>تسجيل الخروج</summary>
	/// <remarks>
	/// يُنهي الجلسة الحالية: رمز الدخول ورمز التحديث يبطلان فوراً.
	/// </remarks>
	/// <response code="200">تم الخروج.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	[HttpPost("logout")]
	[Authorize]
	public async Task<ActionResult<APIResponse>> Logout()
	{
		if (!await _userRepo.LogoutAsync(User))
		{
			return Fail(HttpStatusCode.Unauthorized, "الجلسة غير صالحة أو منتهية.");
		}
		return Success(new { message = "Logged out successfully." });
	}

	private ActionResult<APIResponse>? IsThrottled(string key)
	{
		if (!_loginThrottle.IsBlocked(key, out int retryAfterSeconds))
		{
			return null;
		}
		Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
		return Fail(HttpStatusCode.TooManyRequests, $"محاولات كثيرة فاشلة. حاول بعد {retryAfterSeconds} ثانية.");
	}
}
