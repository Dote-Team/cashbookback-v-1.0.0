using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Dto;
using cashbook.Dto.transactionHistory;
using cashbook.Helper;
using cashbook.Interfaces;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class TransactionHistoryController : ControllerBase
{
	protected APIResponse _response;

	private readonly IMapper _mapper;

	private readonly ITransactionHistoryRepository _transactionHistoryRepository;

	public TransactionHistoryController(ITransactionHistoryRepository transactionHistoryRepository, IMapper mapper)
	{
		_transactionHistoryRepository = transactionHistoryRepository;
		_mapper = mapper;
		_response = new APIResponse();
	}

	[HttpGet("ByBook")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[SwaggerOperation(null, null, Summary = "Get all transaction histories by BookId")]
	public async Task<ActionResult<APIResponse>> GetAllByBookId([FromQuery] Guid bookId, [FromQuery] Guid businessId)
	{
		try
		{
			string[] allowedRoles = Roles.TransactionRead;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			if (!Guid.TryParse(userIdStr, out var userId))
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid user ID." };
				return Unauthorized(_response);
			}
			string userRole = await _transactionHistoryRepository.GetUserRoleAsync(userId, businessId);
			if (string.IsNullOrEmpty(userRole) || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to view transaction history for this book." };
				return Forbid();
			}
			List<TransactionHistoryDto> histories = await _transactionHistoryRepository.GetAllByBookIdAsync(bookId);
			_response.Result = histories;
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

	[HttpGet("ByTransaction")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[SwaggerOperation(null, null, Summary = "Get all transaction histories by TransactionId")]
	public async Task<ActionResult<APIResponse>> GetAllByTransactionId([FromQuery] Guid transactionId, [FromQuery] Guid businessId)
	{
		try
		{
			string[] allowedRoles = Roles.TransactionRead;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			if (!Guid.TryParse(userIdStr, out var userId))
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid user ID." };
				return Unauthorized(_response);
			}
			string userRole = await _transactionHistoryRepository.GetUserRoleAsync(userId, businessId);
			if (string.IsNullOrEmpty(userRole) || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to view transaction history for this transaction." };
				return Forbid();
			}
			List<TransactionHistoryDto> histories = await _transactionHistoryRepository.GetAllByTransactionIdAsync(transactionId);
			_response.Result = histories;
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
}
