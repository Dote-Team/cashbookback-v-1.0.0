using AutoMapper;
using cashbook.Dto;
using cashbook.Models;
using cashbook.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Net;
using cashbook.Dto.contact;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using cashbook.Data;
using Swashbuckle.AspNetCore.Annotations;

namespace cashbook.Controllers
{
    [Route("API/[Controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        protected APIResponse _response;
        private readonly IMapper _mapper;
        private readonly IContactRepository _contactRepository;
        private readonly ApplicationDbContext _context;

        public ContactController(IContactRepository contactRepository, IMapper mapper, ApplicationDbContext context)
        {
            _contactRepository = contactRepository;
            _response = new();
            _mapper = mapper;
            _context = context;
        }

        // GET: API/Contact
        [HttpGet]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "all")]

        public async Task<ActionResult<APIResponse>> GetPaginatedContacts(
      [FromQuery] Guid businessId,
      [FromQuery] int? skip = 1,
      [FromQuery] int? take = 25,
      [FromQuery] string? search = null)
        {
            try
            {
               

                var paginatedContacts = await _contactRepository.GetContactsAsync(
                    businessId,
                    skip,
                    take,
                    search);

                var paginatedResponse = new PaginatedResponse<ContactDto>
                {
                    Data = paginatedContacts.Data,
                    TotalRecords = paginatedContacts.TotalRecords,
                    Skip = paginatedContacts.Skip,
                    Take = paginatedContacts.Take
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

        // GET: API/Contact/{id}
        [HttpGet("{id:Guid}", Name = "GetContact")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> GetContact(Guid id)
        {
            try
            {
                if (id == null)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    return BadRequest(_response);
                }

                var contact = await _contactRepository.GetAsync(u => u.Id == id);
                if (contact == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    return NotFound(_response);
                }

                _response.Result = contact;
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

        // POST: API/Contact
        [HttpPost]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> CreateContact([FromBody] CreateContactDto createContactDto)
        {
            try
            {

                if (createContactDto == null)
                {
                    return BadRequest(createContactDto);
                }
                var contact = _mapper.Map<Contact>(createContactDto);

                await _contactRepository.CreateAsync(contact);
                _response.Result = new { Id = contact.Id };
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

        [HttpDelete("{id:Guid}", Name = "DeleteContact")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> DeleteContact(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid contact ID.");
                }

                var contact = await _contactRepository.GetAsync(u => u.Id == id);
                if (contact == null)
                {
                    return NotFound("Contact not found.");
                }


                await _contactRepository.RemoveAsync(contact);

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


        [HttpPut("{id:Guid}", Name = "UpdateContact")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> UpdateContact([FromRoute] Guid id, [FromBody] UpdateContactDto updateContactDto)
        {
            try
            {
                var existingContact = await _contactRepository.GetAsync(u => u.Id == id);
                _mapper.Map(updateContactDto, existingContact);

                if (updateContactDto == null || existingContact == null)
                {
                    return BadRequest();
                }

                await _contactRepository.UpdateAsync(existingContact);
                _response.Result = existingContact;
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
