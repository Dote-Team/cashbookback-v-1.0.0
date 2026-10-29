using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.userManagement;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>إنشاء المستخدمين وإدارتهم بالتوارث: السوبر أدمن ينشئ أدمن، والأدمن ينشئ مستخدمين بصلاحيات من صلاحياته فقط. المستخدم العادي لا ينشئ أحداً.</summary>
[Route("API/[Controller]")]
[Authorize]
public class UserManagementController : ApiControllerBase
{
	private readonly IUserManagementService _users;

	public UserManagementController(IUserManagementService users)
	{
		_users = users;
	}

	/// <summary>قائمة المستخدمين الذين تديرهم</summary>
	/// <remarks>
	/// **الصلاحية:** سوبر أدمن (يرى الجميع) أو أدمن (يرى من أنشأهم). المستخدم العادي يُرفض.
	///
	/// كل مستخدم معه صلاحياته على كل خزنة (grants) وعدد بصماته.
	/// </remarks>
	/// <param name="businessId">اختياري: من لهم صلاحية على خزائن هذه المنشأة فقط.</param>
	/// <param name="search">بحث في الاسم أو اسم الدخول.</param>
	/// <param name="includeInactive">إظهار المعطّلين أيضاً.</param>
	/// <param name="skip">رقم الصفحة، يبدأ من 1.</param>
	/// <param name="take">عدد السجلات في الصفحة.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	[HttpGet]
	public async Task<ActionResult<APIResponse>> List([FromQuery] Guid? businessId = null, [FromQuery] string? search = null, [FromQuery] bool includeInactive = true, [FromQuery] int? skip = 1, [FromQuery] int? take = 25)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return Success(await _users.ListAsync(callerId, businessId, search, includeInactive, Page(skip), PageSize(take)));
	}

	/// <summary>بيانات مستخدم وصلاحياته</summary>
	/// <remarks>
	/// **الصلاحية:** من أنشأه، أو السوبر أدمن.
	/// </remarks>
	/// <param name="id">معرّف المستخدم.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpGet("{id:Guid}")]
	public async Task<ActionResult<APIResponse>> Get(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		ManagedUserDto? user = await _users.GetAsync(callerId, id);
		return user == null ? Fail(HttpStatusCode.NotFound, "المستخدم غير موجود.") : Success(user);
	}

	/// <summary>إنشاء مستخدم</summary>
	/// <remarks>
	/// **الصلاحية:**
	/// - السوبر أدمن ينشئ **أدمن** (المستوى 2).
	/// - الأدمن ينشئ **مستخدماً** (المستوى 3)، وصلاحياته يجب أن تكون من صلاحيات الأدمن نفسه على الخزائن نفسها.
	/// - المستخدم العادي لا ينشئ أحداً.
	///
	/// grants: قائمة خزائن، ولكل خزنة رموز الصلاحيات (من GET /API/Permission/catalog).
	///
	/// مثال:
	///
	///     {
	///       "username": "ali", "name": "علي", "password": "Ali@12345",
	///       "grants": [ { "bookId": "...", "permissions": ["book.view", "transaction.view", "transaction.create"] } ]
	///     }
	/// </remarks>
	/// <param name="dto">بيانات المستخدم وصلاحياته.</param>
	/// <response code="201">أُنشئ المستخدم.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تستطيع إنشاء مستخدمين، أو منحت صلاحية لا تملكها.</response>
	/// <response code="409">اسم الدخول مستخدم مسبقاً.</response>
	[HttpPost]
	public async Task<ActionResult<APIResponse>> Create([FromBody] CreateManagedUserDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (dto == null)
		{
			return Fail(HttpStatusCode.BadRequest, "بيانات غير صالحة.");
		}
		ServiceResult result = await _users.CreateUserAsync(callerId, dto);
		return result.Ok ? Success(result.Result, HttpStatusCode.Created) : FromResult(result);
	}

	/// <summary>تعديل اسم المستخدم وبريده</summary>
	/// <remarks>
	/// **الصلاحية:** من أنشأه، أو السوبر أدمن. لتغيير الصلاحيات استخدم /permissions.
	/// </remarks>
	/// <param name="id">معرّف المستخدم.</param>
	/// <param name="dto">الاسم والبريد.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPut("{id:Guid}")]
	public async Task<ActionResult<APIResponse>> Update(Guid id, [FromBody] UpdateManagedUserDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.UpdateUserAsync(callerId, id, dto ?? new UpdateManagedUserDto()));
	}

	/// <summary>تحديد صلاحيات المستخدم على الخزائن</summary>
	/// <remarks>
	/// **الصلاحية:** من أنشأه، أو السوبر أدمن.
	///
	/// القائمة المرسلة **تستبدل** صلاحياته على الخزائن المذكورة. الأدمن لا يمنح إلا ما يملكه.
	/// سحب صلاحية من أدمن يسحبها تلقائياً من كل المستخدمين الذين أنشأهم.
	/// خزنة بقائمة فارغة = سحب كل صلاحياته عليها.
	/// </remarks>
	/// <param name="id">معرّف المستخدم.</param>
	/// <param name="dto">الخزائن وصلاحيات كل منها.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPut("{id:Guid}/permissions")]
	public async Task<ActionResult<APIResponse>> SetPermissions(Guid id, [FromBody] UpdateGrantsDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.SetGrantsAsync(callerId, id, dto?.Grants ?? new()));
	}

	/// <summary>تعطيل مستخدم</summary>
	/// <remarks>
	/// **الصلاحية:** من أنشأه، أو السوبر أدمن.
	///
	/// لا حذف في النظام. المعطّل لا يستطيع الدخول، وجلساته تنتهي فوراً، ومن أنشأهم يتعطلون معه.
	/// </remarks>
	/// <param name="id">معرّف المستخدم.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPost("{id:Guid}/deactivate")]
	public async Task<ActionResult<APIResponse>> Deactivate(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.DeactivateAsync(callerId, id));
	}

	/// <summary>إعادة تفعيل مستخدم</summary>
	/// <remarks>
	/// **الصلاحية:** من أنشأه، أو السوبر أدمن. لا يُفعَّل مستخدم والأدمن الذي أنشأه معطّل.
	/// </remarks>
	/// <param name="id">معرّف المستخدم.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	/// <response code="409">الأدمن الذي أنشأه معطّل.</response>
	[HttpPost("{id:Guid}/activate")]
	public async Task<ActionResult<APIResponse>> Activate(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.ActivateAsync(callerId, id));
	}

	/// <summary>تعيين كلمة مرور جديدة لمستخدم</summary>
	/// <remarks>
	/// **الصلاحية:** من أنشأه، أو السوبر أدمن. كل جلسات المستخدم تنتهي فوراً.
	/// </remarks>
	/// <param name="id">معرّف المستخدم.</param>
	/// <param name="dto">كلمة المرور الجديدة (8 أحرف على الأقل، فيها حرف ورقم).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPost("{id:Guid}/reset-password")]
	public async Task<ActionResult<APIResponse>> ResetPassword(Guid id, [FromBody] ResetPasswordDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		return FromResult(await _users.ResetPasswordAsync(callerId, id, dto?.Password ?? ""));
	}
}
