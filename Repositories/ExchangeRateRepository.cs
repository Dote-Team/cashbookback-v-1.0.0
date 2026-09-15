using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Enums;

namespace cashbook.Repositories;

public class ExchangeRateRepository : Repository<ExchangeRate>, IExchangeRateRepository, IRepository<ExchangeRate>
{
	private readonly ApplicationDbContext _context;

	public ExchangeRateRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}

	public async Task<ExchangeRate?> GetForDateAsync(Guid bookId, CurrencyCode currency, DateTime rateDate)
	{
		DateTime date = rateDate.Date;
		return await _context.ExchangeRates.FirstOrDefaultAsync((ExchangeRate er) => er.BookId == bookId && (int)er.Currency == (int)currency && er.RateDate == date);
	}

	public async Task<ExchangeRate?> GetLatestAsync(Guid bookId, CurrencyCode currency)
	{
		return await (from er in _context.ExchangeRates
			where er.BookId == bookId && (int)er.Currency == (int)currency
			orderby er.RateDate descending, er.UpdatedAt descending
			select er).FirstOrDefaultAsync();
	}

	public async Task<ExchangeRate> UpsertAsync(Guid bookId, CurrencyCode currency, DateTime rateDate, decimal rate, Guid userId)
	{
		DateTime date = rateDate.Date;
		ExchangeRate existing = await _context.ExchangeRates.FirstOrDefaultAsync((ExchangeRate er) => er.BookId == bookId && (int)er.Currency == (int)currency && er.RateDate == date);
		if (existing == null)
		{
			existing = new ExchangeRate
			{
				BookId = bookId,
				Currency = currency,
				RateDate = date,
				Rate = rate,
				SetByUserId = userId,
				CreatedAt = DateTime.Now,
				UpdatedAt = DateTime.Now
			};
			_context.ExchangeRates.Add(existing);
		}
		else
		{
			existing.Rate = rate;
			existing.SetByUserId = userId;
			existing.UpdatedAt = DateTime.Now;
		}
		await _context.SaveChangesAsync();
		return existing;
	}

	public async Task<Dictionary<Guid, ExchangeRate>> GetForBooksOnDateAsync(IEnumerable<Guid> bookIds, CurrencyCode currency, DateTime rateDate)
	{
		List<Guid> ids = bookIds?.Distinct().ToList() ?? new List<Guid>();
		DateTime date = rateDate.Date;
		if (!ids.Any())
		{
			return new Dictionary<Guid, ExchangeRate>();
		}
		return (from er in await _context.ExchangeRates.Where((ExchangeRate er) => ids.Contains(er.BookId) && (int)er.Currency == (int)currency && er.RateDate == date).ToListAsync()
			group er by er.BookId).ToDictionary((IGrouping<Guid, ExchangeRate> g) => g.Key, (IGrouping<Guid, ExchangeRate> g) => g.First());
	}

	public async Task<List<ExchangeRate>> GetHistoryAsync(Guid bookId, CurrencyCode currency, DateTime? from = null, DateTime? to = null)
	{
		IQueryable<ExchangeRate> query = _context.ExchangeRates.Where((ExchangeRate er) => er.BookId == bookId && (int)er.Currency == (int)currency);
		if (from.HasValue)
		{
			query = query.Where((ExchangeRate er) => er.RateDate >= ((DateTime?)from).Value.Date);
		}
		if (to.HasValue)
		{
			query = query.Where((ExchangeRate er) => er.RateDate <= ((DateTime?)to).Value.Date);
		}
		return await query.OrderByDescending((ExchangeRate er) => er.RateDate).ToListAsync();
	}
}
