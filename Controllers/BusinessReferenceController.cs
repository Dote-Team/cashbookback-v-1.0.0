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

	/// <summary>لا حذف في النظام: هذا المسار يؤرشف السجل دون رجعة. السبب اختياري.</summary>
	[HttpDelete("{id:Guid}")]
	public Task<ActionResult<APIResponse>> Delete(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

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
