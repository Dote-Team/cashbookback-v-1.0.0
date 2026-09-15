using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.exchangeRate;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Enums;
using cashbook.Services;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
[Authorize]
public class ExchangeRateController : ControllerBase
{
	protected APIResponse _response;

	private readonly IExchangeRateRepository _exchangeRateRepository;

	private readonly IExchangeRateAuthorization _authorization;

	private readonly ApplicationDbContext _context;

	public ExchangeRateController(IExchangeRateRepository exchangeRateRepository, IExchangeRateAuthorization authorization, ApplicationDbContext context)
	{
		_exchangeRateRepository = exchangeRateRepository;
		_authorization = authorization;
		_context = context;
		_response = new APIResponse();
	}

	private bool TryGetUserId(out Guid userId)
	{
		return Guid.TryParse(base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value, out userId);
	}

	private ActionResult<APIResponse> Fail(HttpStatusCode status, string message)
	{
		_response.StatusCode = status;
		_response.IsSuccess = false;
		_response.ErrorMessages = new List<string> { message };
		return StatusCode((int)status, _response);
	}

	[HttpGet("managed")]
	[SwaggerOperation(null, null, Summary = "SuperAdmin , portfolio_manager")]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	public async Task<ActionResult<APIResponse>> GetManaged([FromQuery] Guid businessId)
	{
		try
		{
			if (!TryGetUserId(out var userId))
			{
				return Fail(HttpStatusCode.Unauthorized, "Invalid user ID.");
			}
			bool flag = !(await _context.BusinessUsers.AnyAsync((BusinessUser bu) => bu.BusinessId == businessId && bu.UserId == userId));
			bool flag2 = flag;
			if (flag2)
			{
				flag2 = !(await _authorization.IsSuperAdminAsync(userId));
			}
			if (flag2)
			{
				return Fail(HttpStatusCode.Forbidden, "You are not a member of this business.");
			}
			List<Guid> bookIds = await _authorization.GetManagedBookIdsAsync(userId, businessId);
			DateTime today = DateTime.Today;
			var books = await (from b in _context.Books
				where bookIds.Contains(b.Id)
				select new { b.Id, b.Name } into b
				orderby b.Name
				select b).ToListAsync();
			Dictionary<Guid, ExchangeRate> rates = await _exchangeRateRepository.GetForBooksOnDateAsync(bookIds, CurrencyCode.USD, today);
			List<ExchangeRateStatusDto> result = books.Select(b =>
			{
				bool flag3 = rates.TryGetValue(b.Id, out ExchangeRate value);
				return new ExchangeRateStatusDto
				{
					BookId = b.Id,
					BookName = b.Name,
					Currency = CurrencyCode.USD,
					RateDate = today,
					Rate = (flag3 ? new decimal?(value.Rate) : ((decimal?)null)),
					IsSet = flag3
				};
			}).ToList();
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			_response.Result = result;
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

	[HttpGet("current")]
	[ProducesResponseType(200)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	public async Task<ActionResult<APIResponse>> GetCurrent([FromQuery] Guid bookId, [FromQuery] CurrencyCode currency = CurrencyCode.USD)
	{
		try
		{
			if (!TryGetUserId(out var userId))
			{
				return Fail(HttpStatusCode.Unauthorized, "Invalid user ID.");
			}
			if (!(await _authorization.CanViewAsync(userId, bookId)))
			{
				return Fail(HttpStatusCode.Forbidden, "You do not have permission to view this book.");
			}
			DateTime today = DateTime.Today;
			ExchangeRate todayRate = await _exchangeRateRepository.GetForDateAsync(bookId, currency, today);
			ExchangeRate exchangeRate = todayRate;
			ExchangeRate exchangeRate2 = exchangeRate;
			if (exchangeRate2 == null)
			{
				exchangeRate2 = await _exchangeRateRepository.GetLatestAsync(bookId, currency);
			}
			ExchangeRate effective = exchangeRate2;
			CurrentExchangeRateDto result = new CurrentExchangeRateDto
			{
				BookId = bookId,
				Currency = currency,
				Rate = effective?.Rate,
				RateDate = effective?.RateDate,
				IsToday = (todayRate != null),
				HasValue = (effective != null)
			};
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			_response.Result = result;
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

	[HttpPost]
	[SwaggerOperation(null, null, Summary = "SuperAdmin , portfolio_manager")]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	public async Task<ActionResult<APIResponse>> Upsert([FromBody] UpsertExchangeRateDto dto)
	{
		try
		{
			if (!TryGetUserId(out var userId))
			{
				return Fail(HttpStatusCode.Unauthorized, "Invalid user ID.");
			}
			if (dto == null || dto.BookId == Guid.Empty)
			{
				return Fail(HttpStatusCode.BadRequest, "الخزنة غير محددة.");
			}
			if (!Enum.IsDefined(typeof(CurrencyCode), dto.Currency))
			{
				return Fail(HttpStatusCode.BadRequest, "العملة غير مدعومة — القيم المقبولة: USD أو IQD.");
			}
			if (!(await _authorization.CanManageAsync(userId, dto.BookId)))
			{
				return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية تحديد سعر الصرف لهذه الخزنة.");
			}
			if (!MoneyMath.TryValidateRate(dto.Rate, out string rateError))
			{
				return Fail(HttpStatusCode.BadRequest, rateError);
			}
			DateTime rateDate = (dto.RateDate ?? DateTime.Today).Date;
			ExchangeRate saved = await _exchangeRateRepository.UpsertAsync(dto.BookId, dto.Currency, rateDate, dto.Rate, userId);
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			_response.Result = new ExchangeRateHistoryDto
			{
				Id = saved.Id,
				BookId = saved.BookId,
				Currency = saved.Currency,
				Rate = saved.Rate,
				RateDate = saved.RateDate,
				SetByUserId = saved.SetByUserId,
				CreatedAt = saved.CreatedAt,
				UpdatedAt = saved.UpdatedAt
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

	[HttpGet("history")]
	[ProducesResponseType(200)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	public async Task<ActionResult<APIResponse>> GetHistory([FromQuery] Guid bookId, [FromQuery] CurrencyCode currency = CurrencyCode.USD, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
	{
		try
		{
			if (!TryGetUserId(out var userId))
			{
				return Fail(HttpStatusCode.Unauthorized, "Invalid user ID.");
			}
			if (!(await _authorization.CanViewAsync(userId, bookId)))
			{
				return Fail(HttpStatusCode.Forbidden, "You do not have permission to view this book.");
			}
			List<ExchangeRate> history = await _exchangeRateRepository.GetHistoryAsync(bookId, currency, from, to);
			List<Guid> userIds = (from h in history
				where h.SetByUserId.HasValue
				select h.SetByUserId.Value).Distinct().ToList();
			Dictionary<Guid, string> names = await (from u in _context.Users
				where userIds.Contains(u.Id)
				select new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name);
			List<ExchangeRateHistoryDto> result = history.Select((ExchangeRate h) => new ExchangeRateHistoryDto
			{
				Id = h.Id,
				BookId = h.BookId,
				Currency = h.Currency,
				Rate = h.Rate,
				RateDate = h.RateDate,
				SetByUserId = h.SetByUserId,
				SetByUserName = ((h.SetByUserId.HasValue && names.TryGetValue(h.SetByUserId.Value, out var value)) ? value : null),
				CreatedAt = h.CreatedAt,
				UpdatedAt = h.UpdatedAt
			}).ToList();
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			_response.Result = result;
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
