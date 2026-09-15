using cashbook.Dto;
using cashbook.Dto.business;
using cashbook.Models;
using System.Security.Claims;

namespace cashbook.Interfaces
{
    public interface IBusinessRepository : IRepository<Business>
    {
        Task<bool> CreateBussinessAsync(CreateBusinessDto createBusinessDto, Guid userId);
        Task<PaginatedResponse<BusinessDto>> GetPaginatedBusinessesAsync(
    ClaimsPrincipal userClaims,
    int? skip = 1,
    int? take = 25,
    string search = null);
        Task<BusinessWithBooksDto?> GetBusinessWithBooksDtoByIdAsync(Guid Id);
        Task<bool> DeleteBusinessAsync(Guid businessId);
    }
}
