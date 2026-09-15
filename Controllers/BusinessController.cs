using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Dto;
using cashbook.Dto.business;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class BusinessController : ControllerBase
{
	protected APIResponse _response;

	private readonly IMapper _mapper;

	private readonly IBusinessRepository _businessRepository;

	public BusinessController(IBusinessRepository businessRepository, IMapper mapper)
	{
		_businessRepository = businessRepository;
		_response = new APIResponse();
		_mapper = mapper;
	}

	[HttpGet]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[SwaggerOperation(null, null, Summary = "all")]
	public async Task<ActionResult<APIResponse>> GetPaginatedBusinesses([FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		try
		{
			PaginatedResponse<BusinessDto> paginatedResponse = await _businessRepository.GetPaginatedBusinessesAsync(base.User, skip, take, search);
			_response.Result = paginatedResponse;
			_response.StatusCode = HttpStatusCode.OK;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.StatusCode = HttpStatusCode.InternalServerError;
			_response.ErrorMessages = new List<string> { ex2.ToString() };
			return StatusCode(500, _response);
		}
	}

	[HttpGet("{id:Guid}", Name = "GetBusiness")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ProducesResponseType(400)]
	[SwaggerOperation(null, null, Summary = "all")]
	public async Task<ActionResult<APIResponse>> GetBusiness(Guid id)
	{
		try
		{
			if (id == Guid.Empty)
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid business ID." };
				return BadRequest(_response);
			}
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			if (!Guid.TryParse(userIdStr, out var userId))
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid user ID." };
				return Unauthorized(_response);
			}
			if (await _businessRepository.GetUserRoleAsync(userId, id) == null)
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to view this business." };
				return Forbid();
			}
			BusinessWithBooksDto business = await _businessRepository.GetBusinessWithBooksDtoByIdAsync(id);
			if (business == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Business not found." };
				return NotFound(_response);
			}
			_response.Result = business;
			_response.StatusCode = HttpStatusCode.OK;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.StatusCode = HttpStatusCode.InternalServerError;
			_response.ErrorMessages = new List<string> { ex2.Message };
			return StatusCode(500, _response);
		}
	}

	[HttpPost]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(201)]
	[ProducesResponseType(500)]
	[ProducesResponseType(400)]
	[SwaggerOperation(null, null, Summary = "all")]
	public async Task<ActionResult<APIResponse>> CreateBusiness([FromBody] CreateBusinessDto createBusinessDto)
	{
		try
		{
			if (createBusinessDto == null)
			{
				return BadRequest(createBusinessDto);
			}
			string userIdstring = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			if (!Guid.TryParse(userIdstring, out var userId))
			{
				return Unauthorized("Invalid user ID in token.");
			}
			await _businessRepository.CreateBussinessAsync(createBusinessDto, userId);
			_response.StatusCode = HttpStatusCode.Created;
			_response.IsSuccess = true;
			return _response;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.ToString() };
		}
		return _response;
	}

	[HttpDelete("{id:Guid}", Name = "DeleteBusiness")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(400)]
	[SwaggerOperation(null, null, Summary = "owner")]
	public async Task<ActionResult<APIResponse>> DeleteBusiness(Guid id)
	{
		try
		{
			string[] allowedRoles = Roles.OwnerOnly;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await _businessRepository.GetUserRoleAsync(userId, id);
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to delete business for this business." };
				return Forbid();
			}
			if (id == Guid.Empty)
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.ErrorMessages = new List<string> { "Invalid business ID." };
				return BadRequest(_response);
			}
			if (!(await _businessRepository.DeleteBusinessAsync(id)))
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.ErrorMessages = new List<string> { "Business not found." };
				return NotFound(_response);
			}
			_response.IsSuccess = true;
			_response.StatusCode = HttpStatusCode.NoContent;
			return Ok(_response);
		}
		catch (DbUpdateException)
		{
			// حذف منشأة تحتوي خزائن أو أعضاء أو بيانات مرجعية يخالف قيود المفاتيح الأجنبية.
			// الرمز الصحيح تعارض (409)، ورسالة Entity Framework الخام لا تصلح للعميل.
			_response.IsSuccess = false;
			_response.StatusCode = HttpStatusCode.Conflict;
			_response.ErrorMessages = new List<string>
			{
				"لا يمكن حذف المنشأة لارتباطها ببيانات أخرى: احذف خزائنها وأعضائها وبياناتها المرجعية أولاً."
			};
			return StatusCode(StatusCodes.Status409Conflict, _response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.StatusCode = HttpStatusCode.InternalServerError;
			_response.ErrorMessages = new List<string> { ex2.Message };
			return StatusCode(500, _response);
		}
	}

	[HttpPut("{id:Guid}", Name = "UpdateBusiness")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[SwaggerOperation(null, null, Summary = "owner")]
	public async Task<ActionResult<APIResponse>> UpdateBusiness([FromRoute] Guid id, [FromBody] UpdateBusinessDto updateBusinessDto)
	{
		try
		{
			string[] allowedRoles = Roles.OwnerOnly;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await _businessRepository.GetUserRoleAsync(userId, id);
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to update business for this business." };
				return Forbid();
			}
			Business existingBusiness = await _businessRepository.GetAsync((Business u) => u.Id == id);
			_mapper.Map(updateBusinessDto, existingBusiness);
			if (updateBusinessDto == null || existingBusiness == null)
			{
				return BadRequest();
			}
			await _businessRepository.UpdateAsync(existingBusiness);
			_response.Result = existingBusiness;
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
