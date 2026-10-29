using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.category;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>تصنيفات القيود على مستوى المنشأة. العرض لكل عضو، والإدارة لمن يملك category.manage.</summary>
[Route("API/[Controller]")]
public class CategoryController : BusinessReferenceController<Category, CategoryDto, CreateCategoryDto, UpdateCategoryDto>
{
	private readonly ICategoryRepository _repository;

	public CategoryController(ICategoryRepository repository, IPermissionService permissions)
		: base(repository, permissions)
	{
		_repository = repository;
	}

	protected override string ManagePermission => Permissions.CategoryManage;

	protected override string EntityLabel => "التصنيف";

	protected override Task<PaginatedResponse<CategoryDto>> ListAsync(Guid businessId, int skip, int take, string? search) =>
		_repository.GetCategoriesAsync(businessId, skip, take, search);

	protected override Guid BusinessIdOf(CreateCategoryDto dto) => dto.BusinessId;

	protected override CategoryDto ToDto(Category entity) => new CategoryDto
	{
		Id = entity.Id,
		Name = entity.Name,
		BusinessId = entity.BusinessId,
		CreatedAt = entity.CreatedAt,
		UpdatedAt = entity.UpdatedAt
	};

	protected override string? ValidateCreate(CreateCategoryDto dto) => string.IsNullOrWhiteSpace(dto.Name) ? "الاسم مطلوب." : null;

	protected override Category FromCreate(CreateCategoryDto dto) => new Category { Name = dto.Name.Trim(), BusinessId = dto.BusinessId };

	protected override void ApplyUpdate(UpdateCategoryDto dto, Category entity)
	{
		if (!string.IsNullOrWhiteSpace(dto.Name))
		{
			entity.Name = dto.Name.Trim();
		}
	}
}
