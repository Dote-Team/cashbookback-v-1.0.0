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
using cashbook.Dto.paymentMethod;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class PaymentMethodController : ControllerBase
{
	protected APIResponse _response;

	private readonly IMapper _mapper;

	private readonly IPaymentMethodRepository _paymentMethodRepository;

	private readonly ApplicationDbContext _context;

	public PaymentMethodController(IPaymentMethodRepository paymentMethodRepository, IMapper mapper, ApplicationDbContext context)
	{
		_paymentMethodRepository = paymentMethodRepository;
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
	public async Task<ActionResult<APIResponse>> GetPaginatedPaymentMethods([FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		try
		{
			PaginatedResponse<PaymentMethodDto> paginatedPaymentMethods = await _paymentMethodRepository.GetPaymentMethodsAsync(businessId, skip, take, search);
			PaginatedResponse<PaymentMethodDto> paginatedResponse = new PaginatedResponse<PaymentMethodDto>
			{
				Data = paginatedPaymentMethods.Data,
				TotalRecords = paginatedPaymentMethods.TotalRecords,
				Skip = paginatedPaymentMethods.Skip,
				Take = paginatedPaymentMethods.Take
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

	[HttpGet("{id:Guid}", Name = "GetPaymentMethod")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> GetPaymentMethod(Guid id)
	{
		try
		{
			PaymentMethod paymentMethod = await _paymentMethodRepository.GetAsync((PaymentMethod u) => u.Id == id);
			if (paymentMethod == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				return NotFound(_response);
			}
			_response.Result = paymentMethod;
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
	public async Task<ActionResult<APIResponse>> CreatePaymentMethod([FromBody] CreatePaymentMethodDto createPaymentMethodDto)
	{
		try
		{
			if (createPaymentMethodDto == null)
			{
				return BadRequest(createPaymentMethodDto);
			}
			PaymentMethod paymentMethod = _mapper.Map<PaymentMethod>(createPaymentMethodDto);
			await _paymentMethodRepository.CreateAsync(paymentMethod);
			_response.Result = new { paymentMethod.Id };
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

	[HttpDelete("{id:Guid}", Name = "DeletePaymentMethod")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> DeletePaymentMethod(Guid id)
	{
		try
		{
			if (id == Guid.Empty)
			{
				return BadRequest("Invalid paymentMethod ID.");
			}
			PaymentMethod paymentMethod = await _paymentMethodRepository.GetAsync((PaymentMethod u) => u.Id == id);
			if (paymentMethod == null)
			{
				return NotFound("PaymentMethod not found.");
			}
			await _paymentMethodRepository.RemoveAsync(paymentMethod);
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

	[HttpPut("{id:Guid}", Name = "UpdatePaymentMethod")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> UpdatePaymentMethod([FromRoute] Guid id, [FromBody] UpdatePaymentMethodDto updatePaymentMethodDto)
	{
		try
		{
			PaymentMethod existingPaymentMethod = await _paymentMethodRepository.GetAsync((PaymentMethod u) => u.Id == id);
			_mapper.Map(updatePaymentMethodDto, existingPaymentMethod);
			if (updatePaymentMethodDto == null || existingPaymentMethod == null)
			{
				return BadRequest();
			}
			await _paymentMethodRepository.UpdateAsync(existingPaymentMethod);
			_response.Result = existingPaymentMethod;
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
