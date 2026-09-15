using cashbook.Dto;
using cashbook.Dto.customfield;
using cashbook.Models;

namespace cashbook.Interfaces
{
    public interface ICustomFieldRepository : IRepository<CustomField>
    {
        Task<PaginatedResponse<CustomFieldDto>> GetcustomFieldAsync(
    Guid bookId,
    int? skip = 1,
    int? take = 25,
    string search = null);
    }
}
