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
using cashbook.Dto.contact;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

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
	public async Task<ActionResult<APIResponse>> GetPaginatedContacts([FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		try
		{
			PaginatedResponse<ContactDto> paginatedContacts = await _contactRepository.GetContactsAsync(businessId, skip, take, search);
			PaginatedResponse<ContactDto> paginatedResponse = new PaginatedResponse<ContactDto>
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

	[HttpGet("{id:Guid}", Name = "GetContact")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> GetContact(Guid id)
	{
		try
		{
			Contact contact = await _contactRepository.GetAsync((Contact u) => u.Id == id);
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
	public async Task<ActionResult<APIResponse>> CreateContact([FromBody] CreateContactDto createContactDto)
	{
		try
		{
			if (createContactDto == null)
			{
				return BadRequest(createContactDto);
			}
			Contact contact = _mapper.Map<Contact>(createContactDto);
			await _contactRepository.CreateAsync(contact);
			_response.Result = new { contact.Id };
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

	[HttpDelete("{id:Guid}", Name = "DeleteContact")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> DeleteContact(Guid id)
	{
		try
		{
			if (id == Guid.Empty)
			{
				return BadRequest("Invalid contact ID.");
			}
			Contact contact = await _contactRepository.GetAsync((Contact u) => u.Id == id);
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
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.Message };
			return StatusCode(500, _response);
		}
	}

	[HttpPut("{id:Guid}", Name = "UpdateContact")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> UpdateContact([FromRoute] Guid id, [FromBody] UpdateContactDto updateContactDto)
	{
		try
		{
			Contact existingContact = await _contactRepository.GetAsync((Contact u) => u.Id == id);
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
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.ToString() };
		}
		return _response;
	}
}
