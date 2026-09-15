using System.Linq.Expressions;

namespace cashbook.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null);
        Task<T> GetAsync(Expression<Func<T, bool>> filter = null, bool tracked = true);
        Task CreateAsync(T entiry);
        Task RemoveAsync(T entiry);
        Task UpdateAsync(T entity);
        Task SaveAsync();
        Task<int> GetCountAsync(Expression<Func<T, bool>>? filter = null);
        Task<List<T>> GetPaginatedAsync(int? skip = null, int? take = null, Expression<Func<T, bool>>? filter = null);
        Task<List<string>> UploadFilesAsync(IEnumerable<IFormFile> files, string uploadPath);
        Task<string?> GetUserRoleAsync(Guid userId, Guid businessId);

    }
}
