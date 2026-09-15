using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.user;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class UserController : ControllerBase
{
	private readonly IUserRepository _userRepo;

	protected APIResponse _response;

	private readonly ApplicationDbContext _context;

	private readonly ILoginThrottle _loginThrottle;

	public UserController(IUserRepository userRepo, ApplicationDbContext context, ILoginThrottle loginThrottle)
	{
		_userRepo = userRepo;
		_response = new APIResponse();
		_context = context;
		_loginThrottle = loginThrottle;
	}

	private string ThrottleKey(string? username)
	{
		string text = base.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
		return (username ?? string.Empty).Trim().ToLowerInvariant() + "|" + text;
	}

	private Task<bool> IsMemberAsync(Guid userId, Guid businessId)
	{
		return _context.BusinessUsers.AnyAsync((BusinessUser bu) => bu.UserId == userId && bu.BusinessId == businessId);
	}

	[HttpPost("register")]
	[ProducesResponseType(409)]
	[ProducesResponseType(201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(500)]
	public async Task<ActionResult<APIResponse>> Register([FromBody] RegisterationRequestDto registerationRequestDto)
	{
		try
		{
			if (registerationRequestDto == null)
			{
				return BadRequest(registerationRequestDto);
			}
			if (string.IsNullOrWhiteSpace(registerationRequestDto.Username))
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages.Add("Username is required.");
				return BadRequest(_response);
			}
			if (!(await _userRepo.IsUsernameAvailableAsync(registerationRequestDto.Username)))
			{
				_response.StatusCode = HttpStatusCode.Conflict;
				_response.IsSuccess = false;
				_response.ErrorMessages.Add("Username already exists");
				return Conflict(_response);
			}
			if (!string.IsNullOrWhiteSpace(registerationRequestDto.Email) && !(await _userRepo.IsUniqueUser(registerationRequestDto.Email.Trim())))
			{
				_response.StatusCode = HttpStatusCode.Conflict;
				_response.IsSuccess = false;
				_response.ErrorMessages.Add("Email already exists");
				return Conflict(_response);
			}
			if (!PasswordPolicy.IsValid(registerationRequestDto.Password, out string passwordError))
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages.Add(passwordError);
				return BadRequest(_response);
			}
			UserDto registeredUser = await _userRepo.Register(registerationRequestDto);
			_response.Result = registeredUser;
			_response.StatusCode = HttpStatusCode.Created;
			return CreatedAtRoute("GetUserById", new
			{
				id = registeredUser.Id
			}, _response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.ToString() };
		}
		return _response;
	}

	[HttpPost("login")]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	public async Task<ActionResult<APIResponse>> Login([FromBody] LoginRequestDto loginRequestDto)
	{
		try
		{
			if (loginRequestDto == null)
			{
				return BadRequest(loginRequestDto);
			}
			string key = ThrottleKey(loginRequestDto.Username ?? loginRequestDto.Email);
			if (_loginThrottle.IsBlocked(key, out var retryAfterSeconds))
			{
				base.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
				_response.StatusCode = HttpStatusCode.TooManyRequests;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { $"Too many failed attempts. Try again in {retryAfterSeconds} seconds." };
				return StatusCode(429, _response);
			}
			LoginResponseDto loginResponseDto = await _userRepo.Login(loginRequestDto);
			if (loginResponseDto == null)
			{
				_loginThrottle.RecordFailure(key);
				return Unauthorized();
			}
			_loginThrottle.Reset(key);
			_response.Result = loginResponseDto;
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

	[Authorize]
	[HttpGet]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[SwaggerOperation(null, null, Summary = "all")]
	public async Task<ActionResult<APIResponse>> GetPaginatedUsers([FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		try
		{
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			if (!(await IsMemberAsync(callerId.Value, businessId)))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You are not a member of this business." };
				return StatusCode(403, _response);
			}
			PaginatedResponse<UserDto> paginatedUsers = await _userRepo.GetAllUsersAsync(businessId, skip, take, search);
			_response.Result = paginatedUsers;
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
			_response.ErrorMessages = new List<string> { ex3.Message };
			return StatusCode(500, _response);
		}
	}

	[Authorize]
	[HttpGet("{id}", Name = "GetUserById")]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	public async Task<ActionResult<APIResponse>> GetUserById(Guid id, Guid businessId)
	{
		try
		{
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			if (!(await IsMemberAsync(callerId.Value, businessId)))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You are not a member of this business." };
				return StatusCode(403, _response);
			}
			UserDto user = await _userRepo.GetUserByIdAsync(id, businessId);
			if (user == null)
			{
				return NotFound();
			}
			_response.Result = user;
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

	[Authorize]
	[HttpDelete("{id}")]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	[SwaggerOperation(null, null, Summary = "owner , partner")]
	public async Task<ActionResult<APIResponse>> DeleteUser(Guid id, Guid businessId)
	{
		try
		{
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			string userRole = await _userRepo.GetUserRoleAsync(callerId.Value, businessId);
			if (userRole == null || !Enumerable.Contains(Roles.Management, userRole.ToLowerInvariant()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to delete user for this business." };
				return StatusCode(403, _response);
			}
			List<BusinessUser> memberships = await _context.BusinessUsers.Where((BusinessUser bu) => bu.BusinessId == businessId && bu.UserId == id).ToListAsync();
			if (memberships.Count == 0)
			{
				return NotFound();
			}
			if (memberships.Any((BusinessUser m) => string.Equals(m.Role, "owner", StringComparison.OrdinalIgnoreCase)) && !(await (from u in _context.Users
				where u.Id == ((Guid?)callerId).Value
				select u.IsSuperAdmin).FirstOrDefaultAsync()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Only the super admin can remove the business owner." };
				return StatusCode(403, _response);
			}
			_context.BusinessUsers.RemoveRange(memberships);
			await _context.SaveChangesAsync();
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

	[Authorize]
	[HttpPatch("{id}")]
	[ProducesResponseType(204)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> UpdateUser([FromRoute] Guid id, [FromForm] UserUpdateDto userUpdateDto)
	{
		try
		{
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			User existingUser = await _userRepo.GetAsync((User u) => u.Id == id);
			if (existingUser == null)
			{
				return NotFound();
			}
			if (id != existingUser.Id)
			{
				return BadRequest();
			}
			bool isSelf = callerId.Value == id;
			if (!isSelf)
			{
				bool isSuperAdmin = await (from u in _context.Users
					where u.Id == ((Guid?)callerId).Value
					select u.IsSuperAdmin).FirstOrDefaultAsync();
				bool isManagerOfSharedBusiness = false;
				if (!isSuperAdmin)
				{
					List<Guid> managedBusinesses = await (from bu in _context.BusinessUsers
						where bu.UserId == ((Guid?)callerId).Value && (bu.Role.ToLower() == "owner" || bu.Role.ToLower() == "partner")
						select bu.BusinessId).ToListAsync();
					bool flag = managedBusinesses.Count > 0;
					bool flag2 = flag;
					if (flag2)
					{
						flag2 = await _context.BusinessUsers.AnyAsync((BusinessUser bu) => bu.UserId == id && managedBusinesses.Contains(bu.BusinessId));
					}
					isManagerOfSharedBusiness = flag2;
				}
				if (!isSuperAdmin && !isManagerOfSharedBusiness)
				{
					_response.StatusCode = HttpStatusCode.Forbidden;
					_response.IsSuccess = false;
					_response.ErrorMessages = new List<string> { "You do not have permission to update this user." };
					return StatusCode(403, _response);
				}
			}
			if (!string.IsNullOrWhiteSpace(userUpdateDto.Password) && !PasswordPolicy.IsValid(userUpdateDto.Password, out string passwordError))
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { passwordError };
				return BadRequest(_response);
			}
			if (!isSelf && !string.IsNullOrWhiteSpace(userUpdateDto.Username) && !(await (from u in _context.Users
				where u.Id == ((Guid?)callerId).Value
				select u.IsSuperAdmin).FirstOrDefaultAsync()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Only the super admin can change another user's username." };
				return StatusCode(403, _response);
			}
			if (!(await _userRepo.UpdateUserAsync(id, userUpdateDto)))
			{
				return NotFound();
			}
			_response.StatusCode = HttpStatusCode.NoContent;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.ToString() };
			return StatusCode(500, _response);
		}
	}

	[HttpPost("refresh-token")]
	[ProducesResponseType(204)]
	public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto refreshTokenRequest)
	{
		if (refreshTokenRequest == null || string.IsNullOrEmpty(refreshTokenRequest.RefreshToken))
		{
			return BadRequest("Invalid client request");
		}
		LoginResponseDto response = await _userRepo.RefreshTokenAsync(refreshTokenRequest.RefreshToken);
		if (response == null)
		{
			return Unauthorized("Invalid or expired refresh token");
		}
		return Ok(response);
	}

	[HttpPost("logout")]
	[Authorize]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(500)]
	public async Task<ActionResult<APIResponse>> Logout()
	{
		try
		{
			if (!(await _userRepo.LogoutAsync(base.User)))
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.ErrorMessages = new List<string> { "Invalid or expired session." };
				return Unauthorized(_response);
			}
			_response.IsSuccess = true;
			_response.StatusCode = HttpStatusCode.OK;
			_response.Result = new
			{
				message = "Logged out successfully."
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
