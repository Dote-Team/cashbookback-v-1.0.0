using AutoMapper;
using cashbook.Dto;
using cashbook.Models;
using cashbook.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Net;
using cashbook.Dto.paymentMethod;
using cashbook.Dto.paymentMethod;
using cashbook.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using cashbook.Data;
using Swashbuckle.AspNetCore.Annotations;

namespace cashbook.Controllers
{
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
            _response = new();
            _mapper = mapper;
            _context = context;
        }

        // GET: API/PaymentMethod
        [HttpGet]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "all")]

        public async Task<ActionResult<APIResponse>> GetPaginatedPaymentMethods(
      [FromQuery] Guid businessId,
      [FromQuery] int? skip = 1,
      [FromQuery] int? take = 25,
      [FromQuery] string? search = null)
        {
            try
            {


                var paginatedPaymentMethods = await _paymentMethodRepository.GetPaymentMethodsAsync(
                    businessId,
                    skip,
                    take,
                    search);

                var paginatedResponse = new PaginatedResponse<PaymentMethodDto>
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

        // GET: API/PaymentMethod/{id}
        [HttpGet("{id:Guid}", Name = "GetPaymentMethod")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> GetPaymentMethod(Guid id)
        {
            try
            {
                if (id == null)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    return BadRequest(_response);
                }

                var paymentMethod = await _paymentMethodRepository.GetAsync(u => u.Id == id);
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        // POST: API/PaymentMethod
        [HttpPost]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> CreatePaymentMethod([FromBody] CreatePaymentMethodDto createPaymentMethodDto)
        {
            try
            {

                if (createPaymentMethodDto == null)
                {
                    return BadRequest(createPaymentMethodDto);
                }
                var paymentMethod = _mapper.Map<PaymentMethod>(createPaymentMethodDto);

                await _paymentMethodRepository.CreateAsync(paymentMethod);
                _response.Result = new { Id = paymentMethod.Id };
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

        [HttpDelete("{id:Guid}", Name = "DeletePaymentMethod")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> DeletePaymentMethod(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid paymentMethod ID.");
                }

                var paymentMethod = await _paymentMethodRepository.GetAsync(u => u.Id == id);
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(500, _response);
            }
        }


        [HttpPut("{id:Guid}", Name = "UpdatePaymentMethod")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<APIResponse>> UpdatePaymentMethod([FromRoute] Guid id, [FromBody] UpdatePaymentMethodDto updatePaymentMethodDto)
        {
            try
            {
                var existingPaymentMethod = await _paymentMethodRepository.GetAsync(u => u.Id == id);
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
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }
    }
}
