using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;
using System.Security.Claims;

namespace cashbook.Controllers
{
    [Route("API/[Controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepo;
        protected APIResponse _response;
        private readonly ApplicationDbContext _context;



        public UserController(IUserRepository userRepo, ApplicationDbContext context)
        {
            _userRepo = userRepo;
            _response = new APIResponse();
            _context = context;
        }

        // ✅ Register a new user
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<APIResponse>> Register([FromBody] RegisterationRequestDto registerationRequestDto)
        {
            try
            {
                if (registerationRequestDto == null)
                {
                    return BadRequest(registerationRequestDto);
                }

                string email = registerationRequestDto.Email;
                bool isUnique = await _userRepo.IsUniqueUser(email);

                if (!isUnique)
                {
                    _response.StatusCode = HttpStatusCode.Conflict;
                    _response.IsSuccess = false;
                    _response.ErrorMessages.Add("User already exists");
                    return Conflict(_response);
                }

                UserDto registeredUser = await _userRepo.Register(registerationRequestDto);
                _response.Result = registeredUser;
                _response.StatusCode = HttpStatusCode.Created;

                return CreatedAtRoute("GetUserById", new { id = registeredUser.Id }, _response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        // ✅ Login a user
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<APIResponse>> Login([FromBody] LoginRequestDto loginRequestDto)
        {
            try
            {
                if (loginRequestDto == null)
                {
                    return BadRequest(loginRequestDto);
                }

                LoginResponseDto loginResponseDto = await _userRepo.Login(loginRequestDto);
                if (loginResponseDto == null)
                {
                    return Unauthorized();
                }

                _response.Result = loginResponseDto;
                _response.StatusCode = HttpStatusCode.OK;

                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }


        [Authorize]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(Summary = "all")]

        public async Task<ActionResult<APIResponse>> GetPaginatedUsers(
    [FromQuery] Guid businessId,
    [FromQuery] int? skip = 1,
    [FromQuery] int? take = 25,
    [FromQuery] string? search = null)
        {
            try
            {




                var paginatedUsers = await _userRepo.GetAllUsersAsync(
                    businessId,
                    skip,
                    take,
                    search);

                _response.Result = paginatedUsers;
                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;

                return Ok(_response);
            }
            catch (UnauthorizedAccessException uaex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.Forbidden;
                _response.ErrorMessages = new List<string> { uaex.Message };
                return StatusCode(StatusCodes.Status403Forbidden, _response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }




        // ✅ Get a specific user by ID with their permissions
        [Authorize]
        [HttpGet("{id}", Name = "GetUserById")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<APIResponse>> GetUserById(Guid id, Guid businessId)
        {
            try
            {
                var user = await _userRepo.GetUserByIdAsync(id, businessId);
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        // ✅ Delete a user and their permissions
        [Authorize]
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(Summary = "owner , partner")]

        public async Task<ActionResult<APIResponse>> DeleteUser(Guid id, Guid businessId)
        {
            try
            {

                var allowedRoles = new List<string> { "owner", "partner" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _userRepo.GetUserRoleAsync(userId, businessId);


                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to delete user for this business." };
                    return Forbid();
                }
                bool result = await _userRepo.RemoveUserAsync(id);
                if (!result)
                {
                    return NotFound();
                }

                _response.StatusCode = HttpStatusCode.NoContent;
                _response.IsSuccess = true;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        // ✅ Update a user
        [Authorize]
        [HttpPatch("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> UpdateUser([FromRoute] Guid id, [FromForm] UserUpdateDto userUpdateDto)
        {
            try
            {
                var existingUser = await _userRepo.GetAsync(u => u.Id == id);
                if (existingUser == null)
                {
                    return NotFound();
                }

                if (id != existingUser.Id)
                {
                    return BadRequest();
                }

                bool result = await _userRepo.UpdateUserAsync(id, userUpdateDto);
                if (!result)
                {
                    return NotFound();
                }

                _response.StatusCode = HttpStatusCode.NoContent;
                _response.IsSuccess = true;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }

        // ✅ Refresh token
        [HttpPost("refresh-token")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto refreshTokenRequest)
        {
            if (refreshTokenRequest == null || string.IsNullOrEmpty(refreshTokenRequest.RefreshToken))
            {
                return BadRequest("Invalid client request");
            }

            var response = await _userRepo.RefreshTokenAsync(refreshTokenRequest.RefreshToken);
            if (response == null)
            {
                return Unauthorized("Invalid or expired refresh token");
            }

            return Ok(response);
        }


        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<APIResponse>> Logout()
        {
            try
            {
                var success = await _userRepo.LogoutAsync(User);

                if (!success)
                {
                    _response.IsSuccess = false;
                    _response.StatusCode = HttpStatusCode.Unauthorized;
                    _response.ErrorMessages = new List<string> { "Invalid or expired session." };
                    return Unauthorized(_response);
                }

                _response.IsSuccess = true;
                _response.StatusCode = HttpStatusCode.OK;
                _response.Result = new { message = "Logged out successfully." };

                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }


    }
}
