using AutoMapper;
using cashbook.Dto;
using cashbook.Models;
using cashbook.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Net;
using cashbook.Dto.transaction;
using cashbook.Repositories;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Dto.Book;
using cashbook.Dto.book;

namespace cashbook.Controllers
{
    [Route("API/[Controller]")]
    [ApiController]
    public class TransactionController : ControllerBase
    {
        protected APIResponse _response;
        private readonly IMapper _mapper;
        private readonly ITransactionRepository _transactionRepository;

        public TransactionController(ITransactionRepository transactionRepository, IMapper mapper)
        {
            _transactionRepository = transactionRepository;
            _response = new();
            _mapper = mapper;
        }

        // GET: API/Transaction
        [HttpGet]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "all")]

        public async Task<ActionResult<APIResponse>> GetPaginatedTransactions(
    [FromQuery] Guid bookId,
    [FromQuery] Guid businessId,
    [FromQuery] int? skip = 1,
    [FromQuery] int? take = 25,
    [FromQuery] decimal? amount = null,
    [FromQuery] string? searchCategory = null,
    [FromQuery] string? searchContact = null,
    [FromQuery] string? searchPaymentMethod = null,
    [FromQuery] string? searchType = null,
    [FromQuery] string? searchUser = null,
    [FromQuery] DateTime? startDate = null,
    [FromQuery] DateTime? endDate = null,
    [FromQuery] SortField? sortBy = null,
    [FromQuery] SortDirection? sortDirection = SortDirection.desc)
        {

            try
            {

                var allowedRoles = new List<string> { "owner", "partner", "viewer", "staff", "admin", "dataoperator" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);


                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to view transaction for this book." };
                    return Forbid();
                }

                var paginatedTransaction = await _transactionRepository.GetAllTransactionsByBookIdAsync(
                    bookId, skip, take, amount, searchCategory, searchContact, searchPaymentMethod, searchType, searchUser, startDate, endDate, sortBy, sortDirection);

                var paginatedResponse = new PaginatedResponse<TransactionDto>
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }





        // GET: API/Transaction/RawByBookId
        [HttpGet("RawByBookId")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "all , without pagination")]
        public async Task<ActionResult<APIResponse>> GetAllTransactionsRawByBookId([FromQuery] Guid bookId, [FromQuery] Guid businessId)
        {
            try
            {
                var allowedRoles = new List<string> { "owner", "partner", "viewer", "staff", "admin", "dataoperator" };

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdStr, out var userId))
                {
                    _response.StatusCode = HttpStatusCode.Unauthorized;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "Invalid user ID." };
                    return Unauthorized(_response);
                }

                var userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);

                if (string.IsNullOrEmpty(userRole) || !allowedRoles.Contains(userRole.ToLowerInvariant()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to view transactions for this book." };
                    return Forbid();
                }

                var transactions = await _transactionRepository.GetAllTransactionsRawByBookIdAsync(bookId);

                _response.Result = transactions;
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


        // GET: API/Transaction/{id}
        [HttpGet("{id:Guid}", Name = "GetTransaction")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> GetTransaction(Guid id)
        {
            try
            {
                if (id == null)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    return BadRequest(_response);
                }

                var transaction = await _transactionRepository.GetAsync(u => u.Id == id);
                if (transaction == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    return NotFound(_response);
                }

                _response.Result = transaction;
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

        // POST: API/Transaction
        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [SwaggerOperation(Summary = "owner, partner, staff, admin, dataoperator")]

        public async Task<ActionResult<APIResponse>> CreateTransaction([FromForm] CreateTransactionDto dto, [FromQuery] Guid businessId)
        {
            try
            {


                var allowedRoles = new List<string> { "owner", "partner", "staff", "admin", "dataoperator" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);


                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
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




                var success = await _transactionRepository.CreateTransactionAsync(dto, userId);

                if (!success)
                {
                    _response.StatusCode = HttpStatusCode.InternalServerError;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "An error occurred while creating the transaction." };
                    return StatusCode(StatusCodes.Status500InternalServerError, _response);
                }

                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;
                _response.Result = new { message = "Transaction created successfully." };

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


        [HttpDelete("{id:Guid}", Name = "DeleteTransaction")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "owner , partner")]

        public async Task<ActionResult<APIResponse>> DeleteTransaction(Guid id, Guid businessId)
        {
            try
            {

                var allowedRoles = new List<string> { "owner", "partner" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);


                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
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

                var transactionExists = await _transactionRepository.GetAsync(u => u.Id == id);
                if (transactionExists == null)


                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "Transaction not found." };
                    return NotFound(_response);
                }

                var deleted = await _transactionRepository.DeleteTransactionAsync(id);
                if (!deleted)
                {
                    _response.StatusCode = HttpStatusCode.InternalServerError;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "Failed to delete transaction." };
                    return StatusCode(StatusCodes.Status500InternalServerError, _response);
                }

                _response.StatusCode = HttpStatusCode.NoContent;
                _response.IsSuccess = true;
                _response.Result = null;
                return NoContent();
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.Message };
                _response.StatusCode = HttpStatusCode.InternalServerError;
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }


        [HttpPut("{id:Guid}", Name = "UpdateTransaction")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "owner , partner")]

        public async Task<ActionResult<APIResponse>> UpdateTransaction(
    [FromRoute] Guid id,
    [FromQuery] Guid businessId,
    [FromForm] UpdateTransactionDto updateTransactionDto)
        {
            try
            {
                var allowedRoles = new List<string> { "owner", "partner" };

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);

                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to update transaction for this business." };
                    return StatusCode((int)HttpStatusCode.Forbidden, _response);
                }



                // Use the repository method that handles attachments and custom fields
                var updateResult = await _transactionRepository.UpdateTransactionAsync(updateTransactionDto, id);

                if (!updateResult)
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.Message };
                _response.StatusCode = HttpStatusCode.InternalServerError;
                return StatusCode((int)HttpStatusCode.InternalServerError, _response);
            }
        }


        [HttpPost("{id:Guid}/DuplicateToBook")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Duplicate a transaction to another book (owner, partner, staff, admin, dataoperator)")]
        public async Task<ActionResult<APIResponse>> DuplicateTransactionToAnotherBook(
    [FromRoute] Guid id,
    [FromQuery] Guid targetBookId,
    [FromQuery] Guid businessId)
        {
            try
            {
                var allowedRoles = new List<string> { "owner", "partner", "staff", "admin", "dataoperator", "privateviewer" };

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdStr, out var userId))
                {
                    _response.StatusCode = HttpStatusCode.Unauthorized;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "Invalid user ID." };
                    return Unauthorized(_response);
                }

                var userRole = await _transactionRepository.GetUserRoleAsync(userId, businessId);
                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
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

                var newTransactionId = await _transactionRepository.DuplicateTransactionToAnotherBookAsync(id, targetBookId);

                _response.IsSuccess = true;
                _response.StatusCode = HttpStatusCode.OK;
                _response.Result = new { NewTransactionId = newTransactionId, Message = "Transaction duplicated successfully." };
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
