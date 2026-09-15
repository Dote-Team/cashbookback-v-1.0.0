using AutoMapper;
using cashbook.Models;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Net;
using cashbook.Dto.business;
using System.Security.Claims;
using cashbook.Repositories;
using Swashbuckle.AspNetCore.Annotations;

namespace cashbook.Controllers
{
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
            _response = new();
            _mapper = mapper;
        }

        // GET: API/Business
        [HttpGet]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "all")]

        public async Task<ActionResult<APIResponse>> GetPaginatedBusinesses(
    [FromQuery] int? skip = 1,
    [FromQuery] int? take = 25,
    [FromQuery] string? search = null)
        {
            try
            {



                var paginatedResponse = await _businessRepository.GetPaginatedBusinessesAsync(User, skip, take, search);

                _response.Result = paginatedResponse;
                _response.StatusCode = HttpStatusCode.OK;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.ErrorMessages = new List<string> { ex.ToString() };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }


        // GET: API/Business/{id}
        [HttpGet("{id:Guid}", Name = "GetBusiness")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "all")]

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

                var business = await _businessRepository.GetBusinessWithBooksDtoByIdAsync(id);
                Console.WriteLine(business);

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
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }


        // POST: API/Business
        [HttpPost]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "all")]

        public async Task<ActionResult<APIResponse>> CreateBusiness([FromBody] CreateBusinessDto createBusinessDto)
        {
            try
            {

                if (createBusinessDto == null)
                {
                    return BadRequest(createBusinessDto);
                }
                var userIdstring = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdstring, out Guid userId))
                    return Unauthorized("Invalid user ID in token.");
                await _businessRepository.CreateBussinessAsync(createBusinessDto, userId);
                _response.StatusCode = HttpStatusCode.Created;
                _response.IsSuccess = true;

                return _response;
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        [HttpDelete("{id:Guid}", Name = "DeleteBusiness")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "owner")]

        public async Task<ActionResult<APIResponse>> DeleteBusiness(Guid id)
        {
            try
            {

                var allowedRoles = new List<string> { "owner" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _businessRepository.GetUserRoleAsync(userId, id);


                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
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

                var success = await _businessRepository.DeleteBusinessAsync(id);

                if (!success)
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
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(500, _response);
            }
        }



        [HttpPut("{id:Guid}", Name = "UpdateBusiness")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "owner")]

        public async Task<ActionResult<APIResponse>> UpdateBusiness([FromRoute] Guid id, [FromBody] UpdateBusinessDto updateBusinessDto)
        {
            try
            {

                var allowedRoles = new List<string> { "owner" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _businessRepository.GetUserRoleAsync(userId, id);


                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to update business for this business." };
                    return Forbid();
                }

                var existingBusiness = await _businessRepository.GetAsync(u => u.Id == id);
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }
    }
}
