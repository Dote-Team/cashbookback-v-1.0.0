using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.fingerprint;
using cashbook.Dto.user;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;
using cashbook.Services.Fingerprint;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
public class UserController : ApiControllerBase
{
	private readonly IUserRepository _userRepo;

	private readonly ILoginThrottle _loginThrottle;

	private readonly IPermissionService _permissions;

	private readonly IUserManagementService _users;

	private readonly IFingerprintService _fingerprints;

	private readonly ITokenService _tokens;

	public UserController(IUserRepository userRepo, ILoginThrottle loginThrottle, IPermissionService permissions, IUserManagementService users, IFingerprintService fingerprints, ITokenService tokens)
	{
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

	/// <summary>التسجيل الذاتي مغلق: المستخدمون يُنشَؤون من داخل النظام فقط (UserManagement).</summary>
	[HttpPost("register")]
	public ActionResult<APIResponse> Register() =>
		Fail(HttpStatusCode.Gone, "إنشاء الحسابات من داخل النظام فقط. تواصل مع مسؤول النظام.");

	[HttpPost("login")]
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

	/// <summary>
	/// الدخول بالبصمة بديلاً عن كلمة المرور: اسم المستخدم + صورة البصمة من البرنامج الوسيط.
	/// المطابقة 1:1 على بصمات هذا المستخدم فقط، وتمر بحماية المحاولات المتكررة نفسها.
	/// </summary>
	[HttpPost("login/fingerprint")]
	public async Task<ActionResult<APIResponse>> LoginWithFingerprint([FromBody] FingerprintLoginDto dto)
	{
		if (dto == null || string.IsNullOrWhiteSpace(dto.Username) || dto.Sample == null)
		{
			return Fail(HttpStatusCode.BadRequest, "اسم المستخدم وصورة البصمة مطلوبان.");
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

	/// <summary>لا حذف للمستخدمين: هذا المسار يعطّل الحساب (ومن أنشأهم إن كان أدمن).</summary>
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

	/// <summary>تعديل الملف الشخصي: للمستخدم نفسه، أو لمن أنشأه. تغيير اسم دخول شخص آخر للسوبر أدمن فقط.</summary>
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

	[HttpPost("refresh-token")]
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
