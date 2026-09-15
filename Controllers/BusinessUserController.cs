using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.businessUser;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class BusinessUserController : ControllerBase
{
	protected APIResponse _response;

	private readonly IMapper _mapper;

	private readonly IBusinessUserRepository _businessUserRepository;

	private readonly ApplicationDbContext _context;

	public BusinessUserController(IBusinessUserRepository businessUserRepository, IMapper mapper, ApplicationDbContext context)
	{
		_businessUserRepository = businessUserRepository;
		_response = new APIResponse();
		_mapper = mapper;
		_context = context;
	}

	[HttpGet]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[SwaggerOperation(null, null, Summary = "owner , partner , viewer")]
	public async Task<ActionResult<APIResponse>> GetPaginatedBusinessUsers([FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		try
		{
			string[] allowedRoles = Roles.MemberRead;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await (from bu in _context.BusinessUsers
				where bu.UserId == userId && bu.BusinessId == businessId
				select bu.Role.ToLower()).FirstOrDefaultAsync();
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to view businessUser for this business." };
				return Forbid();
			}
			PaginatedResponse<BusinessUserDto> paginatedBusinessUsers = await _businessUserRepository.GetBusinessUsersAsync(businessId, skip, take, search);
			PaginatedResponse<BusinessUserDto> paginatedResponse = new PaginatedResponse<BusinessUserDto>
			{
				Data = paginatedBusinessUsers.Data,
				TotalRecords = paginatedBusinessUsers.TotalRecords,
				Skip = paginatedBusinessUsers.Skip,
				Take = paginatedBusinessUsers.Take
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

	[HttpDelete("{id:Guid}", Name = "DeleteBusinessUser")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(400)]
	[SwaggerOperation(null, null, Summary = "owner")]
	public async Task<ActionResult<APIResponse>> DeleteBusinessUser(Guid id, Guid businessId)
	{
		try
		{
			string[] allowedRoles = Roles.OwnerOnly;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await (from bu in _context.BusinessUsers
				where bu.UserId == userId && bu.BusinessId == businessId
				select bu.Role.ToLower()).FirstOrDefaultAsync();
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to delete businessUser for this business." };
				return Forbid();
			}
			if (id == Guid.Empty)
			{
				return BadRequest("Invalid businessUser ID.");
			}
			BusinessUser businessUser = await _businessUserRepository.GetAsync((BusinessUser u) => u.Id == id);
			if (businessUser == null)
			{
				return NotFound("BusinessUser not found.");
			}
			await _businessUserRepository.RemoveAsync(businessUser);
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

	[HttpPut("{id:Guid}", Name = "UpdateBusinessUser")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[SwaggerOperation(null, null, Summary = "owner , partner")]
	public async Task<ActionResult<APIResponse>> UpdateBusinessUser([FromRoute] Guid id, [FromBody] UpdateBusinessUserDto updateBusinessUserDto)
	{
		try
		{
			string[] allowedRoles = Roles.Management;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			if (string.IsNullOrEmpty(userIdStr))
			{
				return Unauthorized(new APIResponse
				{
					StatusCode = HttpStatusCode.Unauthorized,
					IsSuccess = false,
					ErrorMessages = new List<string> { "User identity could not be determined." }
				});
			}
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await (from bu in _context.BusinessUsers
				where bu.UserId == userId && bu.BusinessId == updateBusinessUserDto.BusinessId
				select bu.Role.ToLower()).FirstOrDefaultAsync();
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole))
			{
				return StatusCode(403, new APIResponse
				{
					StatusCode = HttpStatusCode.Forbidden,
					IsSuccess = false,
					ErrorMessages = new List<string> { "You do not have permission to update businessUser for this business." }
				});
			}
			APIResponse response = await _businessUserRepository.UpdateBusinessUserAsync(id, updateBusinessUserDto);
			if (!response.IsSuccess)
			{
				return StatusCode((int)response.StatusCode, response);
			}
			return Ok(response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			return StatusCode(500, new APIResponse
			{
				StatusCode = HttpStatusCode.InternalServerError,
				IsSuccess = false,
				ErrorMessages = new List<string> { ex2.Message }
			});
		}
	}

	[HttpGet("{id:Guid}/details")]
	[Authorize]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> GetBusinessUserDetails(Guid id)
	{
		try
		{
			if (id == Guid.Empty)
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid businessUser ID." };
				return BadRequest(_response);
			}
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "User identity could not be determined." };
				return Unauthorized(_response);
			}
			Guid? targetBusinessId = await _context.BusinessUsers.Where((BusinessUser bu) => bu.Id == id).Select((BusinessUser bu) => (Guid?)bu.BusinessId).FirstOrDefaultAsync();
			if (!targetBusinessId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "BusinessUser not found." };
				return NotFound(_response);
			}
			bool isSelf = await _context.BusinessUsers.AnyAsync((BusinessUser bu) => bu.Id == id && bu.UserId == ((Guid?)callerId).Value);
			bool isMember = await _context.BusinessUsers.AnyAsync((BusinessUser bu) => bu.BusinessId == ((Guid?)targetBusinessId).Value && bu.UserId == ((Guid?)callerId).Value);
			if (!isSelf && !isMember)
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to view this businessUser." };
				return StatusCode(403, _response);
			}
			BusinessUserDto businessUserDto = await _businessUserRepository.GetBusinessUserByIdAsync(id);
			if (businessUserDto == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "BusinessUser not found." };
				return NotFound(_response);
			}
			_response.Result = businessUserDto;
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
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

	[HttpDelete("deleteByBusinessAndUser")]
	[Authorize]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	[SwaggerOperation(null, null, Summary = "Delete all BusinessUser records by BusinessId and UserId (Requires owner role)")]
	public async Task<ActionResult<APIResponse>> DeleteBusinessUsersByBusinessAndUser([FromQuery] Guid businessId, [FromQuery] Guid targetUserId)
	{
		try
		{
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await (from bu in _context.BusinessUsers
				where bu.UserId == userId && bu.BusinessId == businessId
				select bu.Role.ToLower()).FirstOrDefaultAsync();
			if (userRole == null || userRole != "owner")
			{
				return Forbid();
			}
			if (!(await _businessUserRepository.DeleteBusinessUsersRange(businessId, targetUserId)))
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.ErrorMessages = new List<string> { "No BusinessUser records found for the given BusinessId and UserId." };
				return NotFound(_response);
			}
			_response.IsSuccess = true;
			_response.StatusCode = HttpStatusCode.OK;
			_response.Result = new
			{
				Message = "BusinessUser records deleted successfully."
			};
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

	[HttpDelete("{businessUserId:Guid}/books/{bookId:Guid}")]
	[Authorize]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	[SwaggerOperation(null, null, Summary = "Remove a specific Book from BusinessUser's BookIds (Requires owner or partner role)")]
	public async Task<ActionResult<APIResponse>> RemoveBookFromBusinessUser(Guid businessUserId, Guid bookId)
	{
		try
		{
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			if (string.IsNullOrEmpty(userIdStr))
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "User identity could not be determined." };
				return Unauthorized(_response);
			}
			Guid userId = Guid.Parse(userIdStr);
			BusinessUser businessUser = await _businessUserRepository.GetAsync((BusinessUser bu) => bu.Id == businessUserId);
			if (businessUser == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "BusinessUser not found." };
				return NotFound(_response);
			}
			string userRole = await (from bu in _context.BusinessUsers
				where bu.UserId == userId && bu.BusinessId == businessUser.BusinessId
				select bu.Role.ToLower()).FirstOrDefaultAsync();
			string[] allowedRoles = Roles.Management;
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to modify businessUser for this business." };
				return Forbid();
			}
			if (!(await _businessUserRepository.RemoveBookFromBusinessUserAsync(businessUserId, bookId)))
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "BookId not found in BusinessUser's BookIds or removal failed." };
				return BadRequest(_response);
			}
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			_response.Result = new
			{
				BusinessUserId = businessUserId,
				RemovedBookId = bookId
			};
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

	[HttpPost("exchange-owner")]
	[Authorize]
	[ProducesResponseType(200)]
	[ProducesResponseType(403)]
	[ProducesResponseType(400)]
	[ProducesResponseType(404)]
	[SwaggerOperation(null, null, Summary = "Exchange ownership to another user (Requires current owner role)")]
	public async Task<ActionResult<APIResponse>> ExchangeOwner([FromQuery] Guid targetUserId, [FromQuery] Guid businessId)
	{
		try
		{
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			if (string.IsNullOrEmpty(userIdStr))
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.ErrorMessages = new List<string> { "User identity could not be determined." };
				return Unauthorized(_response);
			}
			Guid userId = Guid.Parse(userIdStr);
			BusinessUser currentOwner = await _context.BusinessUsers.Where((BusinessUser bu) => bu.UserId == userId && bu.BusinessId == businessId && bu.Role.ToLower() == "owner").FirstOrDefaultAsync();
			if (currentOwner == null)
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.ErrorMessages = new List<string> { "You are not the current owner of this business." };
				return Forbid();
			}
			if (!(await _businessUserRepository.ExchangeOwnerAsync(currentOwner.Id, targetUserId, businessId)))
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.ErrorMessages = new List<string> { "Exchange owner failed. Make sure the target user exists and you are the current owner." };
				return BadRequest(_response);
			}
			_response.IsSuccess = true;
			_response.StatusCode = HttpStatusCode.OK;
			_response.Result = new
			{
				Message = "Ownership exchanged successfully."
			};
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
}
