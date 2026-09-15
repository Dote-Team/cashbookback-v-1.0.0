using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.book;
using cashbook.Dto.Book;
using cashbook.Dto.business;
using cashbook.Dto.category;
using cashbook.Dto.contact;
using cashbook.Dto.setting;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace cashbook.Repositories
{
    public class BookRepository : Repository<Book>, IBookRepository
    {

        private readonly ApplicationDbContext _context;
        public BookRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
        {
            _context = context;

        }

        public async Task<bool> CreateBookAsync(CreateBookDto createBookDto)
        {
            try
            {
                var book = new Book
                {
                    Name = createBookDto.Name,
                    BusinessId = createBookDto.BusinessId,
                };

                await _context.Books.AddAsync(book);
                await _context.SaveChangesAsync();
                var setting = new Setting
                {
                    BookId = book.Id,
                };

                await _context.Settings.AddAsync(setting);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        public async Task<PaginatedResponse<BookDto>> GetBooksAsync(
     Guid businessId,
     Guid userId,
     int? skip = 1,
     int? take = 25,
     string search = null,
     SortField? sortBy = null,
     SortDirection? sortDirection = SortDirection.desc)
        {
            int currentPage = skip.GetValueOrDefault(1);
            int currentPageSize = take.GetValueOrDefault(25);

            // Get role and book access list for this user in this business
            var businessUser = await _context.BusinessUsers
                .Where(bu => bu.BusinessId == businessId && bu.UserId == userId)
                .Select(bu => new
                {
                    Role = bu.Role.ToLower(),
                    BookIds = bu.BookIds
                })
                .FirstOrDefaultAsync();

            if (businessUser == null)
            {
                return new PaginatedResponse<BookDto>
                {
                    TotalRecords = 0,
                    Skip = currentPage,
                    Take = currentPageSize,
                    Data = new List<BookDto>()
                };
            }

            // Start query with books under this business
            var query = _context.Books
                .Where(b => b.BusinessId == businessId);

            // Restricted roles filter
            if (businessUser.Role is "admin" or "dataoperator" or "staff" or "privateviewer")
            {
                if (businessUser.BookIds == null || !businessUser.BookIds.Any())
                {
                    return new PaginatedResponse<BookDto>
                    {
                        TotalRecords = 0,
                        Skip = currentPage,
                        Take = currentPageSize,
                        Data = new List<BookDto>()
                    };
                }
                query = query.Where(b => businessUser.BookIds.Contains(b.Id));
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(b => b.Name.Contains(search));
            }

            // Fetch raw data including balance calculation
            var booksRaw = await query
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.BusinessId,
                    b.CreatedAt,
                    b.UpdatedAt,
                    CashIn = b.Transactions
                        .Where(t => t.Type.ToLower() == "cash in")
                        .Sum(t => (decimal?)t.Amount) ?? 0,
                    CashOut = b.Transactions
                        .Where(t => t.Type.ToLower() == "cash out")
                        .Sum(t => (decimal?)t.Amount) ?? 0
                })
                .ToListAsync();

            // Map to DTOs with balance
            var books = booksRaw.Select(b => new BookDto
            {
                Id = b.Id,
                Name = b.Name,
                BusinessId = b.BusinessId,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
                Balance = b.CashIn - b.CashOut
            });

            // Apply sorting
            books = (sortBy, sortDirection) switch
            {
                (SortField.Name, SortDirection.asc) => books.OrderBy(b => b.Name),
                (SortField.Name, SortDirection.desc) => books.OrderByDescending(b => b.Name),

                (SortField.CreatedAt, SortDirection.asc) => books.OrderBy(b => b.CreatedAt),
                (SortField.CreatedAt, SortDirection.desc) => books.OrderByDescending(b => b.CreatedAt),

                (SortField.Balance, SortDirection.asc) => books.OrderBy(b => b.Balance),
                (SortField.Balance, SortDirection.desc) => books.OrderByDescending(b => b.Balance),

                _ => books.OrderByDescending(b => b.CreatedAt)
            };

            int totalRecords = books.Count();

            var paginatedBooks = books
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToList();

            return new PaginatedResponse<BookDto>
            {
                TotalRecords = totalRecords,
                Skip = currentPage,
                Take = currentPageSize,
                Data = paginatedBooks
            };
        }





        public async Task<BookDetailsDto?> GetBookDetailsByIdAsync(
    Guid bookId,
    Guid userId)
        {
            var book = await _context.Books
     .FirstOrDefaultAsync(b => b.Id == bookId);


            if (book == null) return null;

            var businessId = book.BusinessId;

            var userBusiness = await _context.BusinessUsers
                .FirstOrDefaultAsync(bu => bu.UserId == userId && bu.BusinessId == businessId);

            if (userBusiness == null) return null;

            var role = userBusiness.Role?.ToLowerInvariant();
            var privilegedRoles = new[] { "owner", "viewer", "partner" };
            var rolesNeedBookCheck = new[] { "admin", "staff", "dataoperator", "privateviewer" };

            if (!privilegedRoles.Contains(role) && rolesNeedBookCheck.Contains(role))
            {
                if (userBusiness.BookIds == null || !userBusiness.BookIds.Contains(bookId))
                    return null;
            }

            var allTransactions = await _context.Transactions
                .Where(t => t.BookId == bookId)
                .ToListAsync();

            decimal runningBalance = 0;
            decimal cashInTotal = 0;
            decimal cashOutTotal = 0;

            foreach (var t in allTransactions)
            {
                if (t.Type.Equals("cash in", StringComparison.InvariantCultureIgnoreCase))
                {
                    runningBalance += t.Amount;
                    cashInTotal += t.Amount;
                }
                else if (t.Type.Equals("cash out", StringComparison.InvariantCultureIgnoreCase))
                {
                    runningBalance -= t.Amount;
                    cashOutTotal += t.Amount;
                }
            }

            var setting = await _context.Settings
    .FirstOrDefaultAsync(s => s.BookId == bookId);


            return new BookDetailsDto
            {
                Id = book.Id,
                Name = book.Name,
                BusinessId = book.BusinessId,
                CashInTotal = cashInTotal,
                CashOutTotal = cashOutTotal,
                Balance = runningBalance,
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt,
                Setting = setting == null ? null : new SettingDto
                {
                    CategoryStatus = setting.CategoryStatus,
                    PaymentMethodStatus = setting.PaymentMethodStatus,
                    ContactStatus = setting.ContactStatus,
                    CreatedAt = setting.CreatedAt,
                    UpdatedAt = setting.UpdatedAt
                }

            };
        }





        public async Task<bool> DeleteBookAsync(Guid bookId)
        {
            var book = await _context.Books
                .Include(b => b.CustomFields)
                .Include(b => b.Transactions)
                    .ThenInclude(t => t.Attachements)
                .Include(b => b.Transactions)
                    .ThenInclude(t => t.CustomFieldValues)
                .FirstOrDefaultAsync(b => b.Id == bookId);

            if (book == null)
                return false;

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            var attachments = book.Transactions
                .SelectMany(t => t.Attachements ?? Enumerable.Empty<Attachement>())
                .ToList();

            foreach (var attachment in attachments)
            {
                if (string.IsNullOrEmpty(attachment.Files))
                    continue;

                var filePath = Path.Combine(uploadsFolder, attachment.Files);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete file {attachment.Files}: {ex.Message}");
                    }
                }
            }

            if (attachments.Any())
                _context.Attachements.RemoveRange(attachments);

            var customFieldValues = book.Transactions
                .SelectMany(t => t.CustomFieldValues ?? Enumerable.Empty<CustomFieldValue>())
                .ToList();

            if (customFieldValues.Any())
                _context.CustomFieldValues.RemoveRange(customFieldValues);

            if (book.Transactions != null && book.Transactions.Any())
                _context.Transactions.RemoveRange(book.Transactions);

            if (book.CustomFields != null && book.CustomFields.Any())
                _context.CustomFields.RemoveRange(book.CustomFields);

            _context.Books.Remove(book);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateSettingAsync(Guid bookId, UpdateSettingDto dto)
        {
            var setting = await _context.Settings.FirstOrDefaultAsync(s => s.BookId == bookId);

            if (setting == null)
                return false;

            if (dto.CategoryStatus.HasValue)
                setting.CategoryStatus = dto.CategoryStatus.Value;

            if (dto.PaymentMethodStatus.HasValue)
                setting.PaymentMethodStatus = dto.PaymentMethodStatus.Value;

            if (dto.ContactStatus.HasValue)
                setting.ContactStatus = dto.ContactStatus.Value;

            setting.UpdatedAt = DateTime.Now;

            _context.Settings.Update(setting);
            await _context.SaveChangesAsync();

            return true;
        }


    }
}
