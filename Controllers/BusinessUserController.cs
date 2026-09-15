using AutoMapper;
using cashbook.Dto;
using cashbook.Models;
using cashbook.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Net;
using cashbook.Dto.businessUser;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using cashbook.Data;
using Swashbuckle.AspNetCore.Annotations;

namespace cashbook.Controllers
{
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
            _response = new();
            _mapper = mapper;
            _context = context;
        }

        // GET: API/BusinessUser
        [HttpGet]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "owner , partner , viewer")]

        public async Task<ActionResult<APIResponse>> GetPaginatedBusinessUsers(
      [FromQuery] Guid businessId,
      [FromQuery] int? skip = 1,
      [FromQuery] int? take = 25,
      [FromQuery] string? search = null)
        {
            try
            {
                var allowedRoles = new List<string> { "owner", "partner", "viewer" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _context.BusinessUsers
           .Where(bu => bu.UserId == userId && bu.BusinessId == businessId)
           .Select(bu => bu.Role.ToLower())
           .FirstOrDefaultAsync();

                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to view businessUser for this business." };
                    return Forbid();
                }

                var paginatedBusinessUsers = await _businessUserRepository.GetBusinessUsersAsync(
                    businessId,
                    skip,
                    take,
                    search);

                var paginatedResponse = new PaginatedResponse<BusinessUserDto>
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
                _response.ErrorMessages = new List<string> { ex.ToString() };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }




        [HttpDelete("{id:Guid}", Name = "DeleteBusinessUser")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "owner")]

        public async Task<ActionResult<APIResponse>> DeleteBusinessUser(Guid id, Guid businessId)
        {
            try
            {

                var allowedRoles = new List<string> { "owner" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _context.BusinessUsers
           .Where(bu => bu.UserId == userId && bu.BusinessId == businessId)
           .Select(bu => bu.Role.ToLower())
           .FirstOrDefaultAsync();

                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
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

                var businessUser = await _businessUserRepository.GetAsync(u => u.Id == id);
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(500, _response);
            }
        }


        [HttpPut("{id:Guid}", Name = "UpdateBusinessUser")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "owner , partner")]

        public async Task<ActionResult<APIResponse>> UpdateBusinessUser([FromRoute] Guid id, [FromBody] UpdateBusinessUserDto updateBusinessUserDto)
        {
            try
            {
                var allowedRoles = new List<string> { "owner", "partner" };

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return Unauthorized(new APIResponse
                    {
                        StatusCode = HttpStatusCode.Unauthorized,
                        IsSuccess = false,
                        ErrorMessages = new List<string> { "User identity could not be determined." }
                    });
                }

                var userId = Guid.Parse(userIdStr);

                var userRole = await _context.BusinessUsers
                    .Where(bu => bu.UserId == userId && bu.BusinessId == updateBusinessUserDto.BusinessId)
                    .Select(bu => bu.Role.ToLower())
                    .FirstOrDefaultAsync();

                if (userRole == null || !allowedRoles.Contains(userRole))
                {
                    return StatusCode((int)HttpStatusCode.Forbidden, new APIResponse
                    {
                        StatusCode = HttpStatusCode.Forbidden,
                        IsSuccess = false,
                        ErrorMessages = new List<string> { "You do not have permission to update businessUser for this business." }
                    });
                }

                // Call the new repository update logic
                var response = await _businessUserRepository.UpdateBusinessUserAsync(id, updateBusinessUserDto);

                if (!response.IsSuccess)
                {
                    return StatusCode((int)response.StatusCode, response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new APIResponse
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    IsSuccess = false,
                    ErrorMessages = new List<string> { ex.Message }
                });
            }
        }

        // GET: API/BusinessUser/{id}/details
        [HttpGet("{id:Guid}/details")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

                var businessUserDto = await _businessUserRepository.GetBusinessUserByIdAsync(id);

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
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }

        [HttpDelete("deleteByBusinessAndUser")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(Summary = "Delete all BusinessUser records by BusinessId and UserId (Requires owner role)")]

        public async Task<ActionResult<APIResponse>> DeleteBusinessUsersByBusinessAndUser(
    [FromQuery] Guid businessId,
    [FromQuery] Guid targetUserId)
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _context.BusinessUsers
                    .Where(bu => bu.UserId == userId && bu.BusinessId == businessId)
                    .Select(bu => bu.Role.ToLower())
                    .FirstOrDefaultAsync();

                if (userRole == null || userRole != "owner")
                {
                    return Forbid();
                }

                var deleted = await _businessUserRepository.DeleteBusinessUsersRange(businessId, targetUserId);

                if (!deleted)
                {
                    _response.IsSuccess = false;
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.ErrorMessages = new List<string> { "No BusinessUser records found for the given BusinessId and UserId." };
                    return NotFound(_response);
                }

                _response.IsSuccess = true;
                _response.StatusCode = HttpStatusCode.OK;
                _response.Result = new { Message = "BusinessUser records deleted successfully." };

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


        [HttpDelete("{businessUserId:Guid}/books/{bookId:Guid}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(Summary = "Remove a specific Book from BusinessUser's BookIds (Requires owner or partner role)")]
        public async Task<ActionResult<APIResponse>> RemoveBookFromBusinessUser(Guid businessUserId, Guid bookId)
        {
            try
            {
                // Get current user id and role for the business user being updated
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                {
                    _response.StatusCode = HttpStatusCode.Unauthorized;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "User identity could not be determined." };
                    return Unauthorized(_response);
                }

                var userId = Guid.Parse(userIdStr);

                // Check if current user has permission on the BusinessUser record
                var businessUser = await _businessUserRepository.GetAsync(bu => bu.Id == businessUserId);
                if (businessUser == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "BusinessUser not found." };
                    return NotFound(_response);
                }

                // Check role of the current user for the relevant business
                var userRole = await _context.BusinessUsers
                    .Where(bu => bu.UserId == userId && bu.BusinessId == businessUser.BusinessId)
                    .Select(bu => bu.Role.ToLower())
                    .FirstOrDefaultAsync();

                var allowedRoles = new List<string> { "owner", "partner" };
                if (userRole == null || !allowedRoles.Contains(userRole))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to modify businessUser for this business." };
                    return Forbid();
                }

                // Call repository to remove the book from BusinessUser.BookIds
                var success = await _businessUserRepository.RemoveBookFromBusinessUserAsync(businessUserId, bookId);

                if (!success)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "BookId not found in BusinessUser's BookIds or removal failed." };
                    return BadRequest(_response);
                }

                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;
                _response.Result = new { BusinessUserId = businessUserId, RemovedBookId = bookId };

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



        [HttpPost("exchange-owner")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(Summary = "Exchange ownership to another user (Requires current owner role)")]
        public async Task<ActionResult<APIResponse>> ExchangeOwner([FromQuery] Guid targetUserId, [FromQuery] Guid businessId)
        {
            try
            {
                // Get current logged in user ID
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                {
                    _response.IsSuccess = false;
                    _response.StatusCode = HttpStatusCode.Unauthorized;
                    _response.ErrorMessages = new List<string> { "User identity could not be determined." };
                    return Unauthorized(_response);
                }

                var userId = Guid.Parse(userIdStr);

                // Get the current business user ID (must be owner)
                var currentOwner = await _context.BusinessUsers
                    .Where(bu => bu.UserId == userId && bu.BusinessId == businessId && bu.Role.ToLower() == "owner")
                    .FirstOrDefaultAsync();

                if (currentOwner == null)
                {
                    _response.IsSuccess = false;
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.ErrorMessages = new List<string> { "You are not the current owner of this business." };
                    return Forbid();
                }

                // Call repository method
                var result = await _businessUserRepository.ExchangeOwnerAsync(currentOwner.Id, targetUserId, businessId);

                if (!result)
                {
                    _response.IsSuccess = false;
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.ErrorMessages = new List<string> { "Exchange owner failed. Make sure the target user exists and you are the current owner." };
                    return BadRequest(_response);
                }

                _response.IsSuccess = true;
                _response.StatusCode = HttpStatusCode.OK;
                _response.Result = new { Message = "Ownership exchanged successfully." };
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
