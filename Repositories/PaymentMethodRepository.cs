using cashbook.Data;
using cashbook.Dto.category;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Dto.paymentMethod;
using Microsoft.EntityFrameworkCore;

namespace cashbook.Repositories
{
    public class PaymentMethodRepository : Repository<PaymentMethod>, IPaymentMethodRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentMethodRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
        {
            _context = context;
        }

        public async Task<PaginatedResponse<PaymentMethodDto>> GetPaymentMethodsAsync(
            Guid businessId,
            int? skip = 1,
            int? take = 25,
            string search = null)
        {
            int currentPage = skip.GetValueOrDefault(1);
            int currentPageSize = take.GetValueOrDefault(25);

            var query = _context.PaymentMethods
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
                .Select(c => new PaymentMethodDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    BusinessId = c.BusinessId,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();

            return new PaginatedResponse<PaymentMethodDto>
            {
                TotalRecords = totalRecords,
                Skip = currentPage,
                Take = currentPageSize,
                Data = books
            };
        }
    }
}
