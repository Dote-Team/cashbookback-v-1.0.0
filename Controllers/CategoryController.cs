using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.category;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
public class CategoryController : BusinessReferenceController<Category, CategoryDto, CreateCategoryDto, UpdateCategoryDto>
{
	private readonly ICategoryRepository _repository;

	public CategoryController(ICategoryRepository repository, IPermissionService permissions, IMapper mapper)
		: base(repository, permissions, mapper)
	{
		_repository = repository;
	}

	protected override string ManagePermission => Permissions.CategoryManage;

	protected override string EntityLabel => "التصنيف";

	protected override Task<PaginatedResponse<CategoryDto>> ListAsync(Guid businessId, int skip, int take, string? search) =>
		_repository.GetCategoriesAsync(businessId, skip, take, search);

	protected override Guid BusinessIdOf(CreateCategoryDto dto) => dto.BusinessId;
}
