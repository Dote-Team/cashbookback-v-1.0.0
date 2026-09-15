using cashbook.Dto.contact;
using cashbook.Dto;
using cashbook.Models;

namespace cashbook.Interfaces
{
    public interface IContactRepository : IRepository<Contact>
    {
        Task<PaginatedResponse<ContactDto>> GetContactsAsync(
    Guid businessId,
    int? skip = 1,
    int? take = 25,
    string search = null);
    }
}
