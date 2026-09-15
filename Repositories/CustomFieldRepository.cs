using cashbook.Data;
using cashbook.Dto.category;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.EntityFrameworkCore;
using cashbook.Dto.customfield;

namespace cashbook.Repositories
{
    public class CustomFieldRepository : Repository<CustomField>, ICustomFieldRepository
    {

        private readonly ApplicationDbContext _context;
        public CustomFieldRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
        {
            _context = context;

        }

        public async Task<PaginatedResponse<CustomFieldDto>> GetcustomFieldAsync(
    Guid bookId,
    int? skip = 1,
    int? take = 25,
    string search = null)
        {

            int currentPage = skip.GetValueOrDefault(1);
            int currentPageSize = take.GetValueOrDefault(25);

            var query = _context.CustomFields
                .Where(b => b.BookId == bookId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(b => b.Key.Contains(search));
            }

            int totalRecords = await query.CountAsync();

            var books = await query
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .Select(c => new CustomFieldDto
                {
                    Id = c.Id,
                    Key = c.Key,
                    IsRequired = c.IsRequired,
                    BookId = c.BookId,
                })
                .ToListAsync();

            return new PaginatedResponse<CustomFieldDto>
            {
                TotalRecords = totalRecords,
                Skip = currentPage,
                Take = currentPageSize,
                Data = books
            };
        }


    }
}
