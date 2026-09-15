using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.Book;
using cashbook.Dto.book;
using cashbook.Dto.setting;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Constants;
using cashbook.Models.Enums;

namespace cashbook.Repositories;

public class BookRepository : Repository<Book>, IBookRepository, IRepository<Book>
{
    private readonly ApplicationDbContext _context;

    public BookRepository(ApplicationDbContext context, IConfiguration configuration)
        : base(context)
    {
        _context = context;
    }

    public async Task<bool> CreateBookAsync(CreateBookDto createBookDto)
    {
        try
        {
            Book book = new Book
            {
                Name = createBookDto.Name,
                BusinessId = createBookDto.BusinessId
            };
            await _context.Books.AddAsync(book);
            await _context.SaveChangesAsync();
            Setting setting = new Setting
            {
                BookId = book.Id
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

    public async Task<PaginatedResponse<BookDto>> GetBooksAsync(Guid businessId, Guid userId, int? skip = 1, int? take = 25, string search = null, SortField? sortBy = null, SortDirection? sortDirection = SortDirection.desc)
    {
        int currentPage = skip ?? 1;
        int currentPageSize = take ?? 25;
        var businessUser = await (from bu in _context.BusinessUsers
                                  where bu.BusinessId == businessId && bu.UserId == userId
                                  select new
                                  {
                                      Role = bu.Role.ToLower(),
                                      BookIds = bu.BookIds
                                  }).FirstOrDefaultAsync();
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
        IQueryable<Book> query = _context.Books.Where((Book b) => b.BusinessId == businessId);
        bool flag;
        switch (businessUser.Role)
        {
            case "admin":
            case "dataoperator":
            case "staff":
            case "privateviewer":
            case "portfolio_manager":
                flag = true;
                break;
            default:
                flag = false;
                break;
        }
        if (flag)
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
            query = query.Where((Book b) => businessUser.BookIds.Contains(b.Id));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where((Book b) => b.Name.Contains(search));
        }
        var booksRaw = from b in query.AsNoTracking()
                       select new BookAggregate
                       {
                           Id = b.Id,
                           Name = b.Name,
                           BusinessId = b.BusinessId,
                           CreatedAt = b.CreatedAt,
                           UpdatedAt = b.UpdatedAt,
                           CashInIqd = (b.Transactions.AsQueryable().Where((Transaction t) => (int)t.Currency == 2 && t.Type == "cash in").Sum((Transaction t) => (decimal?)t.Amount) ?? 0m),
                           CashOutIqd = (b.Transactions.AsQueryable().Where((Transaction t) => (int)t.Currency == 2 && t.Type == "cash out").Sum((Transaction t) => (decimal?)t.Amount) ?? 0m),
                           CashInUsd = (b.Transactions.AsQueryable().Where((Transaction t) => (int)t.Currency == 1 && t.Type == "cash in").Sum((Transaction t) => (decimal?)t.Amount) ?? 0m),
                           CashOutUsd = (b.Transactions.AsQueryable().Where((Transaction t) => (int)t.Currency == 1 && t.Type == "cash out").Sum((Transaction t) => (decimal?)t.Amount) ?? 0m)
                       };
        if (1 == 0)
        {
        }
        IOrderedQueryable<BookAggregate> orderedQueryable;
        switch (sortBy)
        {
            case SortField.Name:
                if (sortDirection.HasValue)
                {
                    SortDirection valueOrDefault = sortDirection.GetValueOrDefault();
                    SortDirection sortDirection2 = valueOrDefault;
                    if (sortDirection2 == SortDirection.asc)
                    {
                        orderedQueryable = booksRaw.OrderBy(b => b.Name);
                        break;
                    }
                    if (sortDirection2 == SortDirection.desc)
                    {
                        orderedQueryable = booksRaw.OrderByDescending(b => b.Name);
                        break;
                    }
                }
                goto default;
            case SortField.CreatedAt:
                if (sortDirection.HasValue)
                {
                    SortDirection valueOrDefault = sortDirection.GetValueOrDefault();
                    SortDirection sortDirection2 = valueOrDefault;
                    if (sortDirection2 == SortDirection.asc)
                    {
                        orderedQueryable = booksRaw.OrderBy(b => b.CreatedAt);
                        break;
                    }
                    if (sortDirection2 == SortDirection.desc)
                    {
                        orderedQueryable = booksRaw.OrderByDescending(b => b.CreatedAt);
                        break;
                    }
                }
                goto default;
            case SortField.Balance:
                if (sortDirection.HasValue)
                {
                    SortDirection valueOrDefault = sortDirection.GetValueOrDefault();
                    SortDirection sortDirection2 = valueOrDefault;
                    if (sortDirection2 == SortDirection.asc)
                    {
                        orderedQueryable = booksRaw.OrderBy(b => b.CashInIqd - b.CashOutIqd);
                        break;
                    }
                    if (sortDirection2 == SortDirection.desc)
                    {
                        orderedQueryable = booksRaw.OrderByDescending(b => b.CashInIqd - b.CashOutIqd);
                        break;
                    }
                }
                goto default;
            default:
                orderedQueryable = booksRaw.OrderByDescending(b => b.CreatedAt);
                break;
        }
        if (1 == 0)
        {
        }
        booksRaw = orderedQueryable;
        int totalRecords = await booksRaw.CountAsync();
        List<BookDto> paginatedBooks = (await booksRaw.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).ToListAsync()).Select(b =>
        {
            decimal num = b.CashInIqd - b.CashOutIqd;
            decimal balanceUsd = b.CashInUsd - b.CashOutUsd;
            return new BookDto
            {
                Id = b.Id,
                Name = b.Name,
                BusinessId = b.BusinessId,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
                BalanceIqd = num,
                BalanceUsd = balanceUsd,
                Balance = num
            };
        }).ToList();
        return new PaginatedResponse<BookDto>
        {
            TotalRecords = totalRecords,
            Skip = currentPage,
            Take = currentPageSize,
            Data = paginatedBooks
        };
    }

    /// <summary>
    /// نتيجة تجميع أرصدة الخزنة لكل عملة — كانت نوعاً مجهولاً، فُصل إلى نوع مُسمّى
    /// لأن مُفكّك التجميع لا يستطيع التعبير عن النوع المجهول، والنوع المُسمّى أوضح على أي حال.
    /// </summary>
    private sealed class BookAggregate
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public Guid BusinessId { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public decimal CashInIqd { get; init; }
        public decimal CashOutIqd { get; init; }
        public decimal CashInUsd { get; init; }
        public decimal CashOutUsd { get; init; }
    }

    public async Task<BookDetailsDto?> GetBookDetailsByIdAsync(Guid bookId, Guid userId)
    {
        Book book = await _context.Books.FirstOrDefaultAsync((Book b) => b.Id == bookId);
        if (book == null)
        {
            return null;
        }
        Guid businessId = book.BusinessId;
        BusinessUser userBusiness = await _context.BusinessUsers.FirstOrDefaultAsync((BusinessUser bu) => bu.UserId == userId && bu.BusinessId == businessId);
        if (userBusiness == null)
        {
            return null;
        }
        string role = userBusiness.Role?.ToLowerInvariant();
        string[] privilegedRoles = Roles.Privileged;
        string[] rolesNeedBookCheck = Roles.BookScoped;
        if (!Enumerable.Contains(privilegedRoles, role) && Enumerable.Contains(rolesNeedBookCheck, role) && (userBusiness.BookIds == null || !userBusiness.BookIds.Contains(bookId)))
        {
            return null;
        }
        var totals = await (from t in _context.Transactions.AsNoTracking()
                            where t.BookId == bookId
                            group t by new { t.Currency, t.Type } into g
                            select new
                            {
                                Currency = g.Key.Currency,
                                Type = g.Key.Type,
                                Sum = g.Sum((Transaction x) => x.Amount)
                            }).ToListAsync();
        decimal cashInTotalIqd = 0m;
        decimal cashOutTotalIqd = 0m;
        decimal cashInTotalUsd = 0m;
        decimal cashOutTotalUsd = 0m;
        foreach (var row in totals)
        {
            int direction = TransactionTypes.Direction(row.Type);
            if (direction == 0)
            {
                continue;
            }
            if (row.Currency == CurrencyCode.USD)
            {
                if (direction > 0)
                {
                    cashInTotalUsd += row.Sum;
                }
                else
                {
                    cashOutTotalUsd += row.Sum;
                }
            }
            else if (direction > 0)
            {
                cashInTotalIqd += row.Sum;
            }
            else
            {
                cashOutTotalIqd += row.Sum;
            }
        }
        decimal balanceIqd = cashInTotalIqd - cashOutTotalIqd;
        decimal balanceUsd = cashInTotalUsd - cashOutTotalUsd;
        Setting setting = await _context.Settings.FirstOrDefaultAsync((Setting s) => s.BookId == bookId);
        return new BookDetailsDto
        {
            Id = book.Id,
            Name = book.Name,
            BusinessId = book.BusinessId,
            CashInTotal = cashInTotalIqd,
            CashOutTotal = cashOutTotalIqd,
            Balance = balanceIqd,
            CashInTotalIqd = cashInTotalIqd,
            CashOutTotalIqd = cashOutTotalIqd,
            BalanceIqd = balanceIqd,
            CashInTotalUsd = cashInTotalUsd,
            CashOutTotalUsd = cashOutTotalUsd,
            BalanceUsd = balanceUsd,
            CreatedAt = book.CreatedAt,
            UpdatedAt = book.UpdatedAt,
            Setting = ((setting == null) ? null : new SettingDto
            {
                CategoryStatus = setting.CategoryStatus,
                PaymentMethodStatus = setting.PaymentMethodStatus,
                ContactStatus = setting.ContactStatus,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            })
        };
    }

    public async Task<bool> DeleteBookAsync(Guid bookId)
    {
        Book book = await _context.Books.Include((Book b) => b.CustomFields).Include((Book b) => b.Transactions).ThenInclude((Transaction t) => t.Attachements)
            .Include((Book b) => b.Transactions)
            .ThenInclude((Transaction t) => t.CustomFieldValues)
            .FirstOrDefaultAsync((Book b) => b.Id == bookId);
        if (book == null)
        {
            return false;
        }
        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        List<Attachement> attachments = book.Transactions.SelectMany(delegate (Transaction t)
        {
            IEnumerable<Attachement> attachements = t.Attachements;
            return attachements ?? Enumerable.Empty<Attachement>();
        }).ToList();
        foreach (Attachement attachment in attachments)
        {
            if (string.IsNullOrEmpty(attachment.Files))
            {
                continue;
            }
            string filePath = Path.Combine(uploadsFolder, attachment.Files);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Exception ex2 = ex;
                    Console.WriteLine("Failed to delete file " + attachment.Files + ": " + ex2.Message);
                }
            }
        }
        if (attachments.Any())
        {
            _context.Attachements.RemoveRange(attachments);
        }
        List<CustomFieldValue> customFieldValues = book.Transactions.SelectMany(delegate (Transaction t)
        {
            IEnumerable<CustomFieldValue> customFieldValues2 = t.CustomFieldValues;
            return customFieldValues2 ?? Enumerable.Empty<CustomFieldValue>();
        }).ToList();
        if (customFieldValues.Any())
        {
            _context.CustomFieldValues.RemoveRange(customFieldValues);
        }
        if (book.Transactions != null && book.Transactions.Any())
        {
            _context.Transactions.RemoveRange(book.Transactions);
        }
        if (book.CustomFields != null && book.CustomFields.Any())
        {
            _context.CustomFields.RemoveRange(book.CustomFields);
        }
        _context.Books.Remove(book);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateSettingAsync(Guid bookId, UpdateSettingDto dto)
    {
        Setting setting = await _context.Settings.FirstOrDefaultAsync((Setting s) => s.BookId == bookId);
        if (setting == null)
        {
            return false;
        }
        if (dto.CategoryStatus.HasValue)
        {
            setting.CategoryStatus = dto.CategoryStatus.Value;
        }
        if (dto.PaymentMethodStatus.HasValue)
        {
            setting.PaymentMethodStatus = dto.PaymentMethodStatus.Value;
        }
        if (dto.ContactStatus.HasValue)
        {
            setting.ContactStatus = dto.ContactStatus.Value;
        }
        setting.UpdatedAt = DateTime.Now;
        _context.Settings.Update(setting);
        await _context.SaveChangesAsync();
        return true;
    }
}
