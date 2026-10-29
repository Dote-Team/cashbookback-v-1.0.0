using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.exchangeRate;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Enums;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>سعر صرف الدولار اليومي لكل خزنة. يُستخدم عند إضافة قيد بالدولار.</summary>
[Route("API/[Controller]")]
[Authorize]
public class ExchangeRateController : ApiControllerBase
{
	private readonly IExchangeRateRepository _exchangeRateRepository;

	private readonly IExchangeRateAuthorization _authorization;

	private readonly ApplicationDbContext _context;

	public ExchangeRateController(IExchangeRateRepository exchangeRateRepository, IExchangeRateAuthorization authorization, ApplicationDbContext context)
	{
		_exchangeRateRepository = exchangeRateRepository;
		_authorization = authorization;
		_context = context;
	}

	/// <summary>خزائني التي أحدد سعر صرفها، وحالة سعر اليوم</summary>
	/// <remarks>
	/// **الصلاحية:** عضو في المنشأة. تُعاد الخزائن التي تملك exchange_rate.manage عليها، مع isSet: هل حُدد سعر اليوم.
	/// </remarks>
	/// <param name="businessId">معرّف المنشأة.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	[HttpGet("managed")]
	public async Task<ActionResult<APIResponse>> GetManaged([FromQuery] Guid businessId)
	{
		if (CallerId is not Guid userId)
		{
			return NoCaller();
		}
		if (!await _context.BusinessUsers.AnyAsync(bu => bu.BusinessId == businessId && bu.UserId == userId) && !await _authorization.IsSuperAdminAsync(userId))
		{
			return Fail(HttpStatusCode.Forbidden, "لست عضواً في هذه المنشأة.");
		}
		List<Guid> bookIds = await _authorization.GetManagedBookIdsAsync(userId, businessId);
		DateTime today = DateTime.Today;
		var books = await _context.Books
			.Where(b => bookIds.Contains(b.Id))
			.OrderBy(b => b.Name)
			.Select(b => new { b.Id, b.Name })
			.ToListAsync();
		Dictionary<Guid, ExchangeRate> rates = await _exchangeRateRepository.GetForBooksOnDateAsync(bookIds, CurrencyCode.USD, today);
		return Success(books.Select(b => new ExchangeRateStatusDto
		{
			BookId = b.Id,
			BookName = b.Name,
			Currency = CurrencyCode.USD,
			RateDate = today,
			Rate = rates.TryGetValue(b.Id, out ExchangeRate? rate) ? rate.Rate : null,
			IsSet = rates.ContainsKey(b.Id)
		}).ToList());
	}

	/// <summary>سعر الصرف الحالي لخزنة</summary>
	/// <remarks>
	/// **الصلاحية:** book.view على الخزنة.
	///
	/// سعر اليوم إن وُجد، وإلا آخر سعر مسجّل (isToday = false). hasValue = false يعني لا سعر إطلاقاً.
	/// </remarks>
	/// <param name="bookId">معرّف الخزنة.</param>
	/// <param name="currency">العملة (USD افتراضياً).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	[HttpGet("current")]
	public async Task<ActionResult<APIResponse>> GetCurrent([FromQuery] Guid bookId, [FromQuery] CurrencyCode currency = CurrencyCode.USD)
	{
		if (CallerId is not Guid userId)
		{
			return NoCaller();
		}
		if (!await _authorization.CanViewAsync(userId, bookId))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية عرض هذه الخزنة.");
		}
		DateTime today = DateTime.Today;
		ExchangeRate? todayRate = await _exchangeRateRepository.GetForDateAsync(bookId, currency, today);
		ExchangeRate? effective = todayRate ?? await _exchangeRateRepository.GetLatestAsync(bookId, currency);
		return Success(new CurrentExchangeRateDto
		{
			BookId = bookId,
			Currency = currency,
			Rate = effective?.Rate,
			RateDate = effective?.RateDate,
			IsToday = todayRate != null,
			HasValue = effective != null
		});
	}

	/// <summary>تحديد سعر الصرف ليوم</summary>
	/// <remarks>
	/// **الصلاحية:** exchange_rate.manage على الخزنة.
	///
	/// إن وُجد سعر لليوم نفسه يُستبدل. rateDate اختياري (اليوم افتراضياً). السعر: دنانير لكل دولار، مثل 1480.
	/// </remarks>
	/// <param name="dto">الخزنة والعملة والسعر والتاريخ.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	[HttpPost]
	public async Task<ActionResult<APIResponse>> Upsert([FromBody] UpsertExchangeRateDto dto)
	{
		if (CallerId is not Guid userId)
		{
			return NoCaller();
		}
		if (dto == null || dto.BookId == Guid.Empty)
		{
			return Fail(HttpStatusCode.BadRequest, "الخزنة غير محددة.");
		}
		if (!Enum.IsDefined(typeof(CurrencyCode), dto.Currency))
		{
			return Fail(HttpStatusCode.BadRequest, "العملة غير مدعومة. القيم المقبولة: USD أو IQD.");
		}
		if (!await _authorization.CanManageAsync(userId, dto.BookId))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية تحديد سعر الصرف لهذه الخزنة.");
		}
		if (!MoneyMath.TryValidateRate(dto.Rate, out string? rateError))
		{
			return Fail(HttpStatusCode.BadRequest, rateError);
		}
		DateTime rateDate = (dto.RateDate ?? DateTime.Today).Date;
		ExchangeRate saved = await _exchangeRateRepository.UpsertAsync(dto.BookId, dto.Currency, rateDate, dto.Rate, userId);
		return Success(new ExchangeRateHistoryDto
		{
			Id = saved.Id,
			BookId = saved.BookId,
			Currency = saved.Currency,
			Rate = saved.Rate,
			RateDate = saved.RateDate,
			SetByUserId = saved.SetByUserId,
			CreatedAt = saved.CreatedAt,
			UpdatedAt = saved.UpdatedAt
		});
	}

	/// <summary>سجل أسعار الصرف</summary>
	/// <remarks>
	/// **الصلاحية:** book.view على الخزنة. مع اسم من حدد كل سعر.
	/// </remarks>
	/// <param name="bookId">معرّف الخزنة.</param>
	/// <param name="currency">العملة (USD افتراضياً).</param>
	/// <param name="from">من تاريخ (اختياري).</param>
	/// <param name="to">إلى تاريخ (اختياري).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	[HttpGet("history")]
	public async Task<ActionResult<APIResponse>> GetHistory([FromQuery] Guid bookId, [FromQuery] CurrencyCode currency = CurrencyCode.USD, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
	{
		if (CallerId is not Guid userId)
		{
			return NoCaller();
		}
		if (!await _authorization.CanViewAsync(userId, bookId))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية عرض هذه الخزنة.");
		}
		List<ExchangeRate> history = await _exchangeRateRepository.GetHistoryAsync(bookId, currency, from, to);
		List<Guid> userIds = history.Where(h => h.SetByUserId.HasValue).Select(h => h.SetByUserId!.Value).Distinct().ToList();
		Dictionary<Guid, string> names = await _context.Users
			.Where(u => userIds.Contains(u.Id))
			.ToDictionaryAsync(u => u.Id, u => u.Name);
		return Success(history.Select(h => new ExchangeRateHistoryDto
		{
			Id = h.Id,
			BookId = h.BookId,
			Currency = h.Currency,
			Rate = h.Rate,
			RateDate = h.RateDate,
			SetByUserId = h.SetByUserId,
			SetByUserName = h.SetByUserId.HasValue && names.TryGetValue(h.SetByUserId.Value, out string? name) ? name : null,
			CreatedAt = h.CreatedAt,
			UpdatedAt = h.UpdatedAt
		}).ToList());
	}
}
