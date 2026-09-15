using AutoMapper;
using cashbook.Dto;
using cashbook.Dto.transactionHistory;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;
using System.Security.Claims;

namespace cashbook.Controllers
{
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
            _response = new();
        }

        // GET: API/TransactionHistory/ByBook
        [HttpGet("ByBook")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "Get all transaction histories by BookId")]
        public async Task<ActionResult<APIResponse>> GetAllByBookId([FromQuery] Guid bookId, [FromQuery] Guid businessId)
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

                // تحقق من صلاحية المستخدم
                var userRole = await _transactionHistoryRepository.GetUserRoleAsync(userId, businessId);
                if (string.IsNullOrEmpty(userRole) || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to view transaction history for this book." };
                    return Forbid();
                }

                var histories = await _transactionHistoryRepository.GetAllByBookIdAsync(bookId);

                _response.Result = histories;
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

        // GET: API/TransactionHistory/ByTransaction
        [HttpGet("ByTransaction")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "Get all transaction histories by TransactionId")]
        public async Task<ActionResult<APIResponse>> GetAllByTransactionId([FromQuery] Guid transactionId, [FromQuery] Guid businessId)
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

                var userRole = await _transactionHistoryRepository.GetUserRoleAsync(userId, businessId);
                if (string.IsNullOrEmpty(userRole) || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to view transaction history for this transaction." };
                    return Forbid();
                }

                var histories = await _transactionHistoryRepository.GetAllByTransactionIdAsync(transactionId);

                _response.Result = histories;
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


    }
}
