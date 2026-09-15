using cashbook.Data;
using cashbook.Dto.contact;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.EntityFrameworkCore;

namespace cashbook.Repositories
{
    public class ContactRepository : Repository<Contact>, IContactRepository
    {
        private readonly ApplicationDbContext _context;


        public ContactRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
        {
            _context = context;



        }

        public async Task<PaginatedResponse<ContactDto>> GetContactsAsync(
            Guid businessId,
            int? skip = 1,
            int? take = 25,
            string search = null)
        {

            int currentPage = skip.GetValueOrDefault(1);
            int currentPageSize = take.GetValueOrDefault(25);

            var query = _context.Contacts
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
                .Select(c => new ContactDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    BusinessId = c.BusinessId,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();

            return new PaginatedResponse<ContactDto>
            {
                TotalRecords = totalRecords,
                Skip = currentPage,
                Take = currentPageSize,
                Data = books
            };
        }
    }
}
