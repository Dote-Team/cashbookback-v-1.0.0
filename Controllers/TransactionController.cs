using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.Book;
using cashbook.Dto.transaction;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Validators;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class TransactionController : ControllerBase
{
	protected APIResponse _response;

	private readonly IMapper _mapper;

	private readonly ITransactionRepository _transactionRepository;

	private readonly ApplicationDbContext _context;

	public TransactionController(ITransactionRepository transactionRepository, IMapper mapper, ApplicationDbContext context)
	{
		_transactionRepository = transactionRepository;
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
	public async Task<ActionResult<APIResponse>> GetPaginatedTransactions([FromQuery] Guid bookId, [FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] decimal? amount = null, [FromQuery] string? searchCategory = null, [FromQuery] string? searchContact = null, [FromQuery] string? searchPaymentMethod = null, [FromQuery] string? searchType = null, [FromQuery] string? searchUser = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] SortField? sortBy = null, [FromQuery] SortDirection? sortDirection = SortDirection.desc)
	{
		try
		{
			string[] allowedRoles = Roles.TransactionRead;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to view transaction for this book." };
				return Forbid();
			}
			PaginatedResponse<TransactionDto> paginatedTransaction = await _transactionRepository.GetAllTransactionsByBookIdAsync(bookId, skip, take, amount, searchCategory, searchContact, searchPaymentMethod, searchType, searchUser, startDate, endDate, sortBy, sortDirection);
			PaginatedResponse<TransactionDto> paginatedResponse = new PaginatedResponse<TransactionDto>
			{
				Data = paginatedTransaction.Data,
				TotalRecords = paginatedTransaction.TotalRecords,
				Skip = paginatedTransaction.Skip,
				Take = paginatedTransaction.Take
			};
			_response.Result = paginatedResponse;
			_response.StatusCode = HttpStatusCode.OK;
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

	[HttpGet("RawByBookId")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[SwaggerOperation(null, null, Summary = "all , without pagination")]
	public async Task<ActionResult<APIResponse>> GetAllTransactionsRawByBookId([FromQuery] Guid bookId, [FromQuery] Guid businessId)
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
			string userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);
			if (string.IsNullOrEmpty(userRole) || !Enumerable.Contains(allowedRoles, userRole.ToLowerInvariant()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to view transactions for this book." };
				return Forbid();
			}
			List<TransactionDto> transactions = await _transactionRepository.GetAllTransactionsRawByBookIdAsync(bookId);
			_response.Result = transactions;
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

	[HttpGet("{id:Guid}", Name = "GetTransaction")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> GetTransaction(Guid id)
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
			Transaction transaction = await _transactionRepository.GetAsync((Transaction u) => u.Id == id);
			if (transaction == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				return NotFound(_response);
			}
			Book book = await _context.Books.AsNoTracking().FirstOrDefaultAsync((Book b) => b.Id == transaction.BookId);
			if (book == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				return NotFound(_response);
			}
			string role = await _transactionRepository.GetUserRoleAsync(callerId.Value, book.BusinessId);
			if (role == null)
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have access to this transaction." };
				return StatusCode(403, _response);
			}
			if (Roles.IsBookScoped(role) && !((await (from bu in _context.BusinessUsers
				where bu.UserId == ((Guid?)callerId).Value && bu.BusinessId == book.BusinessId
				select bu.BookIds).FirstOrDefaultAsync())?.Contains(transaction.BookId) ?? false))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have access to this wallet." };
				return StatusCode(403, _response);
			}
			_response.Result = transaction;
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
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(500)]
	[SwaggerOperation(null, null, Summary = "owner, partner, staff, admin, dataoperator")]
	public async Task<ActionResult<APIResponse>> CreateTransaction([FromForm] CreateTransactionDto dto, [FromQuery] Guid businessId)
	{
		try
		{
			string[] allowedRoles = Roles.Writers;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to create transaction for this business." };
				return Forbid();
			}
			if (dto == null)
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid data." };
				return BadRequest(_response);
			}
			List<string> validationErrors = TransactionValidator.ValidateCreate(dto);
			if (validationErrors.Any())
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = validationErrors;
				return BadRequest(_response);
			}
			if (!(await _transactionRepository.CreateTransactionAsync(dto, userId)))
			{
				_response.StatusCode = HttpStatusCode.InternalServerError;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "An error occurred while creating the transaction." };
				return StatusCode(500, _response);
			}
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			_response.Result = new
			{
				message = "Transaction created successfully."
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

	[HttpDelete("{id:Guid}", Name = "DeleteTransaction")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(400)]
	[SwaggerOperation(null, null, Summary = "owner , partner")]
	public async Task<ActionResult<APIResponse>> DeleteTransaction(Guid id, Guid businessId)
	{
		try
		{
			string[] allowedRoles = Roles.Management;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to view books for this business." };
				return Forbid();
			}
			if (id == Guid.Empty)
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid transaction ID." };
				return BadRequest(_response);
			}
			if (await _transactionRepository.GetAsync((Transaction u) => u.Id == id) == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Transaction not found." };
				return NotFound(_response);
			}
			if (!(await _transactionRepository.DeleteTransactionAsync(id)))
			{
				_response.StatusCode = HttpStatusCode.InternalServerError;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Failed to delete transaction." };
				return StatusCode(500, _response);
			}
			_response.StatusCode = HttpStatusCode.NoContent;
			_response.IsSuccess = true;
			_response.Result = null;
			return NoContent();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.Message };
			_response.StatusCode = HttpStatusCode.InternalServerError;
			return StatusCode(500, _response);
		}
	}

	[HttpPut("{id:Guid}", Name = "UpdateTransaction")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[SwaggerOperation(null, null, Summary = "owner , partner")]
	public async Task<ActionResult<APIResponse>> UpdateTransaction([FromRoute] Guid id, [FromQuery] Guid businessId, [FromForm] UpdateTransactionDto updateTransactionDto)
	{
		try
		{
			string[] allowedRoles = Roles.Management;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			Guid userId = Guid.Parse(userIdStr);
			string userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to update transaction for this business." };
				return StatusCode(403, _response);
			}
			if (updateTransactionDto == null)
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid data." };
				return BadRequest(_response);
			}
			Transaction existingTransaction = await _transactionRepository.GetAsync((Transaction t) => t.Id == id);
			if (existingTransaction == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Transaction not found." };
				return NotFound(_response);
			}
			List<string> validationErrors = TransactionValidator.ValidateUpdate(updateTransactionDto, existingTransaction, out ResolvedTransactionValues _);
			if (validationErrors.Any())
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = validationErrors;
				return BadRequest(_response);
			}
			if (!(await _transactionRepository.UpdateTransactionAsync(updateTransactionDto, id)))
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Transaction not found or update failed." };
				return NotFound(_response);
			}
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.Message };
			_response.StatusCode = HttpStatusCode.InternalServerError;
			return StatusCode(500, _response);
		}
	}

	[HttpPost("{id:Guid}/DuplicateToBook")]
	[Authorize]
	[ProducesResponseType(200)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	[ProducesResponseType(400)]
	[SwaggerOperation(null, null, Summary = "Duplicate a transaction to another book (owner, partner, staff, admin, dataoperator)")]
	public async Task<ActionResult<APIResponse>> DuplicateTransactionToAnotherBook([FromRoute] Guid id, [FromQuery] Guid targetBookId, [FromQuery] Guid businessId)
	{
		try
		{
			string[] allowedRoles = Roles.TransactionDuplicate;
			string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			if (!Guid.TryParse(userIdStr, out var userId))
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid user ID." };
				return Unauthorized(_response);
			}
			string userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);
			if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
			{
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "You do not have permission to duplicate this transaction." };
				return Forbid();
			}
			if (id == Guid.Empty || targetBookId == Guid.Empty)
			{
				_response.StatusCode = HttpStatusCode.BadRequest;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Transaction ID or Target Book ID is invalid." };
				return BadRequest(_response);
			}
			Guid newTransactionId = await _transactionRepository.DuplicateTransactionToAnotherBookAsync(id, targetBookId);
			_response.IsSuccess = true;
			_response.StatusCode = HttpStatusCode.OK;
			_response.Result = new
			{
				NewTransactionId = newTransactionId,
				Message = "Transaction duplicated successfully."
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
