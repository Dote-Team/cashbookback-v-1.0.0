using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using cashbook.Models;
using cashbook.Models.Enums;

namespace cashbook.Interfaces;

public interface IExchangeRateRepository : IRepository<ExchangeRate>
{
	Task<ExchangeRate?> GetForDateAsync(Guid bookId, CurrencyCode currency, DateTime rateDate);

	Task<ExchangeRate?> GetLatestAsync(Guid bookId, CurrencyCode currency);

	Task<ExchangeRate> UpsertAsync(Guid bookId, CurrencyCode currency, DateTime rateDate, decimal rate, Guid userId);

	Task<Dictionary<Guid, ExchangeRate>> GetForBooksOnDateAsync(IEnumerable<Guid> bookIds, CurrencyCode currency, DateTime rateDate);

	Task<List<ExchangeRate>> GetHistoryAsync(Guid bookId, CurrencyCode currency, DateTime? from = null, DateTime? to = null);
}
