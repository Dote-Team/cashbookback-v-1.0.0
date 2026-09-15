using cashbook.Data;
using cashbook.Dto.book;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.EntityFrameworkCore;
using cashbook.Dto.contact;
using cashbook.Dto.category;

namespace cashbook.Repositories
{
    public class CategoryRepository : Repository<Category>, ICategoryRepository
    {

        private readonly ApplicationDbContext _context;
        public CategoryRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
        {
            _context = context;

        }


        public async Task<PaginatedResponse<CategoryDto>> GetCategoriesAsync(
    Guid businessId,
    int? skip = 1,
    int? take = 25,
    string search = null)
        {

            int currentPage = skip.GetValueOrDefault(1);
            int currentPageSize = take.GetValueOrDefault(25);

            var query = _context.Categories
                .Where(b => b.BusinessId == businessId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(b => b.Name.Contains(search));
            }

            int totalRecords = await query.CountAsync();

            var books = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    BusinessId = c.BusinessId,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();

            return new PaginatedResponse<CategoryDto>
            {
                TotalRecords = totalRecords,
                Skip = currentPage,
                Take = currentPageSize,
                Data = books
            };
        }


    }
}
