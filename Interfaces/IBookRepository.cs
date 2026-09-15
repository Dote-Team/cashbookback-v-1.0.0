using System;
using System.Threading.Tasks;
using cashbook.Dto;
using cashbook.Dto.Book;
using cashbook.Dto.book;
using cashbook.Dto.setting;
using cashbook.Models;

namespace cashbook.Interfaces;

public interface IBookRepository : IRepository<Book>
{
	Task<PaginatedResponse<BookDto>> GetBooksAsync(Guid businessId, Guid userId, int? skip = 1, int? take = 25, string search = null, SortField? sortBy = null, SortDirection? sortDirection = SortDirection.desc);

	Task<bool> CreateBookAsync(CreateBookDto createBookDto);

	Task<BookDetailsDto?> GetBookDetailsByIdAsync(Guid bookId, Guid userId);

	Task<bool> DeleteBookAsync(Guid bookId);

	Task<bool> UpdateSettingAsync(Guid bookId, UpdateSettingDto dto);
}
