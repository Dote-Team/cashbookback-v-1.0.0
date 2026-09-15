using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.category;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class CategoryController : ControllerBase
{
	protected APIResponse _response;

	private readonly IMapper _mapper;

	private readonly ICategoryRepository _categoryRepository;

	private readonly ApplicationDbContext _context;

	public CategoryController(ICategoryRepository categoryRepository, IMapper mapper, ApplicationDbContext context)
	{
		_categoryRepository = categoryRepository;
		_response = new APIResponse();
		_mapper = mapper;
		_context = context;
	}

	[HttpGet]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[SwaggerOperation(null, null, Summary = "all")]
	public async Task<ActionResult<APIResponse>> GetPaginatedCategorys([FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		try
		{
			PaginatedResponse<CategoryDto> paginatedCategorys = await _categoryRepository.GetCategoriesAsync(businessId, skip, take, search);
			PaginatedResponse<CategoryDto> paginatedResponse = new PaginatedResponse<CategoryDto>
			{
				Data = paginatedCategorys.Data,
				TotalRecords = paginatedCategorys.TotalRecords,
				Skip = paginatedCategorys.Skip,
				Take = paginatedCategorys.Take
			};
			_response.Result = paginatedResponse;
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (UnauthorizedAccessException ex)
		{
			UnauthorizedAccessException uaex = ex;
			_response.IsSuccess = false;
			_response.StatusCode = HttpStatusCode.Forbidden;
			_response.ErrorMessages = new List<string> { uaex.Message };
			return StatusCode(403, _response);
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex3.ToString() };
			return StatusCode(500, _response);
		}
	}

	[HttpGet("{id:Guid}", Name = "GetCategory")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> GetCategory(Guid id)
	{
		try
		{
			Category category = await _categoryRepository.GetAsync((Category u) => u.Id == id);
			if (category == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				return NotFound(_response);
			}
			_response.Result = category;
			_response.StatusCode = HttpStatusCode.OK;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.ToString() };
		}
		return _response;
	}

	[HttpPost]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(201)]
	[ProducesResponseType(500)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> CreateCategory([FromBody] CreateCategoryDto createCategoryDto)
	{
		try
		{
			if (createCategoryDto == null)
			{
				return BadRequest(createCategoryDto);
			}
			Category category = _mapper.Map<Category>(createCategoryDto);
			await _categoryRepository.CreateAsync(category);
			_response.Result = new { category.Id };
			_response.StatusCode = HttpStatusCode.Created;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.ToString() };
		}
		return _response;
	}

	[HttpDelete("{id:Guid}", Name = "DeleteCategory")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> DeleteCategory(Guid id)
	{
		try
		{
			if (id == Guid.Empty)
			{
				return BadRequest("Invalid category ID.");
			}
			Category category = await _categoryRepository.GetAsync((Category u) => u.Id == id);
			if (category == null)
			{
				return NotFound("Category not found.");
			}
			await _categoryRepository.RemoveAsync(category);
			_response.StatusCode = HttpStatusCode.NoContent;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.Message };
			return StatusCode(500, _response);
		}
	}

	[HttpPut("{id:Guid}", Name = "UpdateCategory")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> UpdateCategory([FromRoute] Guid id, [FromBody] UpdateCategoryDto updateCategoryDto)
	{
		try
		{
			Category existingCategory = await _categoryRepository.GetAsync((Category u) => u.Id == id);
			_mapper.Map(updateCategoryDto, existingCategory);
			if (updateCategoryDto == null || existingCategory == null)
			{
				return BadRequest();
			}
			await _categoryRepository.UpdateAsync(existingCategory);
			_response.Result = existingCategory;
			_response.StatusCode = HttpStatusCode.NoContent;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.ToString() };
		}
		return _response;
	}
}
