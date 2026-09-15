using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto.book;
using cashbook.Dto.transactionHistory;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class TransactionHistoryRepository : Repository<TransactionHistory>, ITransactionHistoryRepository, IRepository<TransactionHistory>
{
	private readonly ApplicationDbContext _context;

	public TransactionHistoryRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}

	public async Task<List<TransactionHistoryDto>> GetAllByBookIdAsync(Guid bookId)
	{
		return (await (from th in _context.TransactionHistories.Where((TransactionHistory th) => th.BookId == bookId).Include((TransactionHistory th) => th.Book).Include((TransactionHistory th) => th.User)
			orderby th.CreatedAt descending
			select th).ToListAsync()).Select((TransactionHistory th) => new TransactionHistoryDto
		{
			Id = th.Id,
			Operation = th.Operation,
			Description = th.Description,
			Amount = th.Amount,
			Type = th.Type,
			ExchangeRate = th.ExchangeRate,
			ExchangeDate = th.ExchangeDate,
			TransactionId = th.TransactionId,
			From = (th.From.HasValue ? th.From.Value : 0m),
			To = (th.To.HasValue ? th.To.Value : 0m),
			CreatedAt = th.CreatedAt,
			BookId = th.BookId,
			Book = ((th.Book == null) ? null : new BookDto
			{
				Id = th.Book.Id,
				Name = th.Book.Name,
				CreatedAt = th.Book.CreatedAt,
				UpdatedAt = th.Book.UpdatedAt
			}),
			UserId = th.UserId,
			User = ((th.User == null) ? null : new UserDto
			{
				Id = th.User.Id,
				Name = th.User.Name,
				Email = th.User.Email,
				CreatedAt = th.User.CreatedAt,
				UpdatedAt = th.User.UpdatedAt
			})
		}).ToList();
	}

	public async Task<List<TransactionHistoryDto>> GetAllByTransactionIdAsync(Guid transactionId)
	{
		return (await (from th in _context.TransactionHistories.Where((TransactionHistory th) => th.TransactionId == transactionId).Include((TransactionHistory th) => th.Book).Include((TransactionHistory th) => th.User)
			orderby th.CreatedAt descending
			select th).ToListAsync()).Select((TransactionHistory th) => new TransactionHistoryDto
		{
			Id = th.Id,
			Operation = th.Operation,
			Description = th.Description,
			Amount = th.Amount,
			Type = th.Type,
			ExchangeRate = th.ExchangeRate,
			ExchangeDate = th.ExchangeDate,
			TransactionId = th.TransactionId,
			From = (th.From.HasValue ? th.From.Value : 0m),
			To = (th.To.HasValue ? th.To.Value : 0m),
			CreatedAt = th.CreatedAt,
			BookId = th.BookId,
			Book = ((th.Book == null) ? null : new BookDto
			{
				Id = th.Book.Id,
				Name = th.Book.Name,
				CreatedAt = th.Book.CreatedAt,
				UpdatedAt = th.Book.UpdatedAt
			}),
			UserId = th.UserId,
			User = ((th.User == null) ? null : new UserDto
			{
				Id = th.User.Id,
				Name = th.User.Name,
				Email = th.User.Email,
				CreatedAt = th.User.CreatedAt,
				UpdatedAt = th.User.UpdatedAt
			})
		}).ToList();
	}
}
