using AutoMapper;
using cashbook.Dto;
using cashbook.Models;
using cashbook.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Net;
using cashbook.Dto.customfield;
using cashbook.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using cashbook.Data;
using Swashbuckle.AspNetCore.Annotations;

namespace cashbook.Controllers
{
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
            _response = new();
            _mapper = mapper;
            _context = context;
        }

        // GET: API/CustomField
        [HttpGet]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "all,except viewer")]

        public async Task<ActionResult<APIResponse>> GetPaginatedCustomField(
      [FromQuery] Guid bookId,
      [FromQuery] int? skip = 1,
      [FromQuery] int? take = 25,
      [FromQuery] string? search = null)
        {
            try
            {
                var allowedRoles = new List<string> { "owner", "partner", "staff", "admin", "dataoperator" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _context.BusinessUsers
           .Where(bu => bu.UserId == userId //&& bu.BookId == bookId
                                            )
           .Select(bu => bu.Role.ToLower())
           .FirstOrDefaultAsync();

                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to view customField for this business." };
                    return Forbid();
                }

                var paginatedCustomField = await _customFieldRepository.GetcustomFieldAsync(
                    bookId,
                    skip,
                    take,
                    search);

                var paginatedResponse = new PaginatedResponse<CustomFieldDto>
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


        // GET: API/CustomField/{id}
        [HttpGet("{id:Guid}", Name = "GetCustomField")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> GetCustomField(Guid id)
        {
            try
            {
                if (id == null)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    return BadRequest(_response);
                }

                var customField = await _customFieldRepository.GetAsync(u => u.Id == id);
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        // POST: API/CustomField
        [HttpPost]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> CreateCustomField([FromBody] CreateCustomFieldDto createCustomFieldDto)
        {
            try
            {

                if (createCustomFieldDto == null)
                {
                    return BadRequest(createCustomFieldDto);
                }
                var customField = _mapper.Map<CustomField>(createCustomFieldDto);

                await _customFieldRepository.CreateAsync(customField);
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

        [HttpDelete("{id:Guid}", Name = "DeleteCustomField")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> DeleteCustomField(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid customField ID.");
                }

                var customField = await _customFieldRepository.GetAsync(u => u.Id == id);
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(500, _response);
            }
        }

        [HttpPut("{id:Guid}", Name = "UpdateCustomField")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> UpdateCustomField([FromRoute] Guid id, [FromBody] UpdateCustomFieldDto updateCustomFieldDto)
        {
            try
            {
                var existingCustomField = await _customFieldRepository.GetAsync(u => u.Id == id);
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

    }
}
