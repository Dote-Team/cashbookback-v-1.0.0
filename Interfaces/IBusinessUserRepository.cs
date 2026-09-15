using cashbook.Dto;
using cashbook.Dto.businessUser;
using cashbook.Models;

namespace cashbook.Interfaces
{
    public interface IBusinessUserRepository : IRepository<BusinessUser>
    {
        Task<PaginatedResponse<BusinessUserDto>> GetBusinessUsersAsync(
            Guid businessId,
            int? skip = 1,
            int? take = 25,
            string search = null);
        Task<BusinessUserDto> GetBusinessUserByIdAsync(Guid businessUserId);
        Task<APIResponse> UpdateBusinessUserAsync(Guid businessUserId, UpdateBusinessUserDto dto);
        Task<bool> DeleteBusinessUsersRange(Guid businessId, Guid userId);
        Task<bool> RemoveBookFromBusinessUserAsync(Guid businessUserId, Guid bookId);
        Task<bool> ExchangeOwnerAsync(Guid currentOwnerId, Guid targetUserId, Guid businessId);
    }
}
