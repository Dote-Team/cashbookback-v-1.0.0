using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.transaction;
using cashbook.Dto.user;
using cashbook.Dto.book;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.EntityFrameworkCore;
using cashbook.Dto.transactionHistory;

namespace cashbook.Repositories
{
    public class TransactionHistoryRepository : Repository<TransactionHistory>, ITransactionHistoryRepository
    {
        private readonly ApplicationDbContext _context;

        public TransactionHistoryRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
        {
            _context = context;
        }

        public async Task<List<TransactionHistoryDto>> GetAllByBookIdAsync(Guid bookId)
        {
            var histories = await _context.TransactionHistories
                .Where(th => th.BookId == bookId)
                .Include(th => th.Book)
                .Include(th => th.User)
                .OrderByDescending(th => th.CreatedAt)
                .ToListAsync();

            return histories.Select(th => new TransactionHistoryDto
            {
                Id = th.Id,
                Operation = th.Operation,
                Description = th.Description,
                Amount = th.Amount,
                Type = th.Type,
                From = th.From.HasValue ? th.From.Value : 0,
                To = th.To.HasValue ? th.To.Value : 0,
                CreatedAt = th.CreatedAt,
                BookId = th.BookId,
                Book = th.Book == null ? null : new BookDto
                {
                    Id = th.Book.Id,
                    Name = th.Book.Name,
                    CreatedAt = th.Book.CreatedAt,
                    UpdatedAt = th.Book.UpdatedAt
                },
                UserId = th.UserId,
                User = th.User == null ? null : new UserDto
                {
                    Id = th.User.Id,
                    Name = th.User.Name,
                    Email = th.User.Email,
                    CreatedAt = th.User.CreatedAt,
                    UpdatedAt = th.User.UpdatedAt
                }
            }).ToList();
        }

        public async Task<List<TransactionHistoryDto>> GetAllByTransactionIdAsync(Guid transactionId)
        {


            var histories = await _context.TransactionHistories
                .Where(th => th.TransactionId == transactionId)
                .Include(th => th.Book)
                .Include(th => th.User)
                .OrderByDescending(th => th.CreatedAt)
                .ToListAsync();

            return histories.Select(th => new TransactionHistoryDto
            {
                Id = th.Id,
                Operation = th.Operation,
                Description = th.Description,
                Amount = th.Amount,
                Type = th.Type,
                From = th.From.HasValue ? th.From.Value : 0,
                To = th.To.HasValue ? th.To.Value : 0,
                CreatedAt = th.CreatedAt,
                BookId = th.BookId,
                Book = th.Book == null ? null : new BookDto
                {
                    Id = th.Book.Id,
                    Name = th.Book.Name,
                    CreatedAt = th.Book.CreatedAt,
                    UpdatedAt = th.Book.UpdatedAt
                },
                UserId = th.UserId,
                User = th.User == null ? null : new UserDto
                {
                    Id = th.User.Id,
                    Name = th.User.Name,
                    Email = th.User.Email,
                    CreatedAt = th.User.CreatedAt,
                    UpdatedAt = th.User.UpdatedAt
                }
            }).ToList();
        }
    }
}
