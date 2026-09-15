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
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.customfield;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class CustomFieldController : ControllerBase
{
	protected APIResponse _response;

	private readonly IMapper _mapper;

	private readonly ICustomFieldRepository _customFieldRepository;

	private readonly ApplicationDbContext _context;

	public CustomFieldController(ICustomFieldRepository customFieldRepository, IMapper mapper, ApplicationDbContext context)
	{
		_customFieldRepository = customFieldRepository;
		_response = new APIResponse();
		_mapper = mapper;
		_context = context;
	}

	[HttpGet]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[SwaggerOperation(null, null, Summary = "all,except viewer")]
	public async Task<ActionResult<APIResponse>> GetPaginatedCustomField([FromQuery] Guid bookId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		try
		{
			string[] allowedRoles = Roles.Writers;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await (from bu in _context.BusinessUsers
				where bu.UserId == userId
				select bu.Role.ToLower()).FirstOrDefaultAsync();
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to view customField for this business." };
				return Forbid();
			}
			PaginatedResponse<CustomFieldDto> paginatedCustomField = await _customFieldRepository.GetcustomFieldAsync(bookId, skip, take, search);
			PaginatedResponse<CustomFieldDto> paginatedResponse = new PaginatedResponse<CustomFieldDto>
			{
				Data = paginatedCustomField.Data,
				TotalRecords = paginatedCustomField.TotalRecords,
				Skip = paginatedCustomField.Skip,
				Take = paginatedCustomField.Take
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

	[HttpGet("{id:Guid}", Name = "GetCustomField")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> GetCustomField(Guid id)
	{
		try
		{
			CustomField customField = await _customFieldRepository.GetAsync((CustomField u) => u.Id == id);
			if (customField == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				return NotFound(_response);
			}
			_response.Result = customField;
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
	public async Task<ActionResult<APIResponse>> CreateCustomField([FromBody] CreateCustomFieldDto createCustomFieldDto)
	{
		try
		{
			if (createCustomFieldDto == null)
			{
				return BadRequest(createCustomFieldDto);
			}
			CustomField customField = _mapper.Map<CustomField>(createCustomFieldDto);
			await _customFieldRepository.CreateAsync(customField);
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

	[HttpDelete("{id:Guid}", Name = "DeleteCustomField")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> DeleteCustomField(Guid id)
	{
		try
		{
			if (id == Guid.Empty)
			{
				return BadRequest("Invalid customField ID.");
			}
			CustomField customField = await _customFieldRepository.GetAsync((CustomField u) => u.Id == id);
			if (customField == null)
			{
				return NotFound("CustomField not found.");
			}
			await _customFieldRepository.RemoveAsync(customField);
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

	[HttpPut("{id:Guid}", Name = "UpdateCustomField")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> UpdateCustomField([FromRoute] Guid id, [FromBody] UpdateCustomFieldDto updateCustomFieldDto)
	{
		try
		{
			CustomField existingCustomField = await _customFieldRepository.GetAsync((CustomField u) => u.Id == id);
			_mapper.Map(updateCustomFieldDto, existingCustomField);
			if (updateCustomFieldDto == null || existingCustomField == null)
			{
				return BadRequest();
			}
			await _customFieldRepository.UpdateAsync(existingCustomField);
			_response.Result = existingCustomField;
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
