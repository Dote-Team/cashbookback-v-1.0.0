using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>
/// عمليات موحّدة للبيانات المرجعية على مستوى المنشأة. القراءة لكل من له صلاحية على أي خزنة
/// في المنشأة (يحتاجها لاختيار التصنيف عند إضافة قيد)، والإدارة لمن يملك صلاحية الإدارة.
/// المنشأة تُستخرج من السجل نفسه عند التعديل والأرشفة، لا من الطلب.
/// التحويل بين الكيان والـ DTO صريح في كل متحكم: التعديل يغيّر الحقول المرسلة فقط،
/// ولا يمحو حقلاً لم يُرسل كما كان يفعل AutoMapper.
/// </summary>
[Authorize]
public abstract class BusinessReferenceController<TEntity, TDto, TCreate, TUpdate> : ApiControllerBase
	where TEntity : class, IBusinessScoped, IArchivable
{
	protected readonly IRepository<TEntity> Repository;

	protected readonly IPermissionService Access;

	protected BusinessReferenceController(IRepository<TEntity> repository, IPermissionService permissions)
	{
		Repository = repository;
		Access = permissions;
	}

	protected abstract string ManagePermission { get; }

	protected abstract string EntityLabel { get; }

	protected abstract Task<PaginatedResponse<TDto>> ListAsync(Guid businessId, int skip, int take, string? search);

	protected abstract Guid BusinessIdOf(TCreate dto);

	protected abstract TDto ToDto(TEntity entity);

	/// <summary>رسالة الخطأ إن كانت بيانات الإضافة ناقصة، وإلا null.</summary>
	protected abstract string? ValidateCreate(TCreate dto);

	protected abstract TEntity FromCreate(TCreate dto);

	/// <summary>ينسخ الحقول المرسلة فقط إلى الكيان. المنشأة لا تتغير.</summary>
	protected abstract void ApplyUpdate(TUpdate dto, TEntity entity);

	/// <summary>القائمة (صفحات)</summary>
	/// <remarks>
	/// **الصلاحية:** عضو في المنشأة (يحتاجها لاختيار القيمة عند إضافة قيد). المؤرشف لا يظهر.
	/// </remarks>
	/// <param name="businessId">معرّف المنشأة.</param>
	/// <param name="skip">رقم الصفحة، يبدأ من 1.</param>
	/// <param name="take">عدد السجلات في الصفحة (حد أقصى 200).</param>
	/// <param name="search">بحث في الاسم.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	[HttpGet]
	public async Task<ActionResult<APIResponse>> GetPaginated([FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (!await Access.IsBusinessMemberAsync(callerId, businessId))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية على هذه المنشأة.");
		}
		return Success(await ListAsync(businessId, Page(skip), PageSize(take), search));
	}

	/// <summary>سجل واحد</summary>
	/// <remarks>
	/// **الصلاحية:** عضو في منشأة السجل.
	/// </remarks>
	/// <param name="id">معرّف السجل.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpGet("{id:Guid}")]
	public async Task<ActionResult<APIResponse>> GetById(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		TEntity? entity = await Repository.GetAsync(e => e.Id == id && !e.IsArchived, tracked: false);
		if (entity == null)
		{
			return Fail(HttpStatusCode.NotFound, EntityLabel + " غير موجود.");
		}
		if (!await Access.IsBusinessMemberAsync(callerId, entity.BusinessId))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية على هذه المنشأة.");
		}
		return Success(ToDto(entity));
	}

	/// <summary>إضافة</summary>
	/// <remarks>
	/// **الصلاحية:** صلاحية الإدارة الخاصة بهذا النوع (category.manage أو contact.manage أو payment_method.manage) على أي خزنة في المنشأة.
	///
	/// الاسم إلزامي.
	/// </remarks>
	/// <param name="dto">الاسم والمنشأة (ورقم الهاتف لجهة الاتصال).</param>
	/// <response code="201">أُضيف. المعرّف في result.id.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	[HttpPost]
	public async Task<ActionResult<APIResponse>> Create([FromBody] TCreate dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (dto == null)
		{
			return Fail(HttpStatusCode.BadRequest, "بيانات غير صالحة.");
		}
		if (ValidateCreate(dto) is { } error)
		{
			return Fail(HttpStatusCode.BadRequest, error);
		}
		if (!await Access.HasInBusinessAsync(callerId, BusinessIdOf(dto), ManagePermission))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية إضافة " + EntityLabel + " في هذه المنشأة.");
		}
		TEntity entity = FromCreate(dto);
		await Repository.CreateAsync(entity);
		return Success(new { entity.Id }, HttpStatusCode.Created);
	}

	/// <summary>تعديل</summary>
	/// <remarks>
	/// **الصلاحية:** صلاحية الإدارة الخاصة بهذا النوع.
	///
	/// أرسل الحقل الذي تريد تغييره فقط: الحقل غير المرسل يبقى كما هو.
	/// </remarks>
	/// <param name="id">معرّف السجل.</param>
	/// <param name="dto">الحقول المراد تغييرها.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPut("{id:Guid}")]
	public async Task<ActionResult<APIResponse>> Update([FromRoute] Guid id, [FromBody] TUpdate dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (dto == null)
		{
			return Fail(HttpStatusCode.BadRequest, "بيانات غير صالحة.");
		}
		TEntity? entity = await Repository.GetAsync(e => e.Id == id && !e.IsArchived);
		if (entity == null)
		{
			return Fail(HttpStatusCode.NotFound, EntityLabel + " غير موجود.");
		}
		if (!await Access.HasInBusinessAsync(callerId, entity.BusinessId, ManagePermission))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية تعديل " + EntityLabel + ".");
		}
		ApplyUpdate(dto, entity);
		entity.UpdatedAt = DateTime.UtcNow;
		await Repository.UpdateAsync(entity);
		return Success(ToDto(entity));
	}

	/// <summary>أرشفة (لا حذف)</summary>
	/// <remarks>
	/// **الصلاحية:** صلاحية الإدارة الخاصة بهذا النوع.
	///
	/// يختفي من القوائم، والقيود القديمة تبقى محتفظة باسمه. مثل POST /{id}/archive.
	/// </remarks>
	/// <param name="id">معرّف السجل.</param>
	/// <param name="reason">سبب الأرشفة (اختياري).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpDelete("{id:Guid}")]
	public Task<ActionResult<APIResponse>> Delete(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

	/// <summary>أرشفة</summary>
	/// <remarks>
	/// **الصلاحية:** صلاحية الإدارة الخاصة بهذا النوع. دون رجعة.
	/// </remarks>
	/// <param name="id">معرّف السجل.</param>
	/// <param name="reason">سبب الأرشفة (اختياري).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPost("{id:Guid}/archive")]
	public Task<ActionResult<APIResponse>> Archive(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

	private async Task<ActionResult<APIResponse>> ArchiveCore(Guid id, string? reason)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		TEntity? entity = await Repository.GetAsync(e => e.Id == id && !e.IsArchived);
		if (entity == null)
		{
			return Fail(HttpStatusCode.NotFound, EntityLabel + " غير موجود.");
		}
		if (!await Access.HasInBusinessAsync(callerId, entity.BusinessId, ManagePermission))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية أرشفة " + EntityLabel + ".");
		}
		await Repository.ArchiveAsync(entity, callerId, reason);
		return Success(new { archived = true });
	}
}
