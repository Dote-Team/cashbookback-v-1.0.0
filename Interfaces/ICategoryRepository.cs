using cashbook.Dto;
using cashbook.Dto.Book;
using cashbook.Dto.category;
using cashbook.Models;

namespace cashbook.Interfaces
{
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<PaginatedResponse<CategoryDto>> GetCategoriesAsync(
    Guid businessId,
    int? skip = 1,
    int? take = 25,
    string search = null);
    }
}
