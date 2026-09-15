using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.book;
using cashbook.Dto.business;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Threading.Tasks;

namespace cashbook.Repositories
{
    public class BusinessRepository : Repository<Business>, IBusinessRepository
    {

        private readonly ApplicationDbContext _context;
        public BusinessRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
        {
            _context = context;

        }

        public async Task<bool> CreateBussinessAsync(CreateBusinessDto createBusinessDto, Guid userId)
        {
            try
            {
                var business = new Business
                {
                    Name = createBusinessDto.Name
                };

                await _context.Businesses.AddAsync(business);
                await _context.SaveChangesAsync();

                var businessUser = new BusinessUser
                {
                    UserId = userId,
                    BusinessId = business.Id,
                    Role = "owner"
                };

                await _context.BusinessUsers.AddAsync(businessUser);

                var paymentMethods = new List<PaymentMethod>
        {
            new PaymentMethod { Name = "Cash", BusinessId = business.Id },
            new PaymentMethod { Name = "Online", BusinessId = business.Id }
        };

                await _context.PaymentMethods.AddRangeAsync(paymentMethods);

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<PaginatedResponse<BusinessDto>> GetPaginatedBusinessesAsync(
    ClaimsPrincipal userClaims,
    int? skip = 1,
    int? take = 25,
    string search = null)
        {
            int currentPage = skip.GetValueOrDefault(1);
            int currentSize = take.GetValueOrDefault(25);
            var userIdStr = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userIdStr, out Guid userId))
            {
                return new PaginatedResponse<BusinessDto>
                {
                    TotalRecords = 0,
                    Skip = skip ?? 1,
                    Take = take ?? 25,
                    Data = new List<BusinessDto>()
                };
            }

            var query = _context.BusinessUsers
                .Include(bu => bu.Business)
                .Where(bu => bu.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(bu => bu.Business.Name.Contains(search));
            }

            int totalRecords = await query.CountAsync();

            var businesses = await query
                .Skip((currentPage - 1) * currentSize)
                .Take(currentSize)
                .Select(bu => new BusinessDto
                {
                    Id = bu.Business.Id,
                    Name = bu.Business.Name,
                    CreatedAt = bu.Business.CreatedAt,
                    UpdatedAt = bu.Business.UpdatedAt,
                    Role = bu.Role,
                    BooksCount = _context.Books.Count(b => b.BusinessId == bu.Business.Id)

                })
                .ToListAsync();

            return new PaginatedResponse<BusinessDto>
            {
                TotalRecords = totalRecords,
                Skip = currentPage,
                Take = currentSize,
                Data = businesses
            };
        }

        public async Task<BusinessWithBooksDto?> GetBusinessWithBooksDtoByIdAsync(Guid Id)
        {
            var business = await _context.Businesses
                .Include(b => b.Books)
                .FirstOrDefaultAsync(b => b.Id == Id);

            if (business == null) return null;

            return new BusinessWithBooksDto
            {
                Id = business.Id,
                Name = business.Name,
                CreatedAt = business.CreatedAt,
                UpdatedAt = business.UpdatedAt,
                Books = business.Books.Select(book => new BookDto
                {
                    Id = book.Id,
                    Name = book.Name,
                    CreatedAt = book.CreatedAt,
                    UpdatedAt = book.UpdatedAt,
                }).ToList()
            };
        }


        public async Task<bool> DeleteBusinessAsync(Guid businessId)
        {
            var business = await _context.Businesses
                .Include(b => b.Books)
                    .ThenInclude(book => book.Transactions)
                        .ThenInclude(t => t.Attachements)
                .Include(b => b.BusinessUsers)
                .Include(b => b.Categories)
                .Include(b => b.Contacts)
                .Include(b => b.PaymentMethods)
                .Include(b => b.Books)
                    .ThenInclude(book => book.CustomFields)
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null)
                return false;

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            var attachments = business.Books
                .SelectMany(book => book.Transactions ?? Enumerable.Empty<Transaction>())
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

            var transactionIds = business.Books
                .SelectMany(book => book.Transactions ?? Enumerable.Empty<Transaction>())
                .Select(t => t.Id)
                .ToList();

            var customFieldValuesToDelete = await _context.CustomFieldValues
                .Where(cfVal => transactionIds.Contains(cfVal.TransactionId))
                .ToListAsync();

            if (customFieldValuesToDelete.Any())
                _context.CustomFieldValues.RemoveRange(customFieldValuesToDelete);

            if (attachments.Any())
                _context.Attachements.RemoveRange(attachments);

            var transactions = business.Books
                .SelectMany(book => book.Transactions ?? Enumerable.Empty<Transaction>())
                .ToList();

            if (transactions.Any())
                _context.Transactions.RemoveRange(transactions);

            var customFields = business.Books
                .SelectMany(book => book.CustomFields ?? Enumerable.Empty<CustomField>())
                .ToList();

            if (customFields.Any())
                _context.CustomFields.RemoveRange(customFields);

            if (business.Books != null && business.Books.Any())
                _context.Books.RemoveRange(business.Books);

            if (business.BusinessUsers != null && business.BusinessUsers.Any())
                _context.BusinessUsers.RemoveRange(business.BusinessUsers);

            if (business.Categories != null && business.Categories.Any())
                _context.Categories.RemoveRange(business.Categories);

            if (business.PaymentMethods != null && business.PaymentMethods.Any())
                _context.PaymentMethods.RemoveRange(business.PaymentMethods);

            if (business.Contacts != null && business.Contacts.Any())
                _context.Contacts.RemoveRange(business.Contacts);

            _context.Businesses.Remove(business);

            await _context.SaveChangesAsync();

            return true;
        }




    }
}
