using cashbook.Dto.Book;
using cashbook.Dto;
using cashbook.Dto.transaction;
using cashbook.Models;

namespace cashbook.Interfaces
{
    public interface ITransactionRepository : IRepository<Transaction>
    {
        Task<bool> CreateTransactionAsync(CreateTransactionDto dto,Guid userId);
        Task<bool> DeleteTransactionAsync(Guid transactionId);
        Task<bool> UpdateTransactionAsync(UpdateTransactionDto dto,Guid Id);
        Task<PaginatedResponse<TransactionDto>> GetAllTransactionsByBookIdAsync(
   Guid bookId,
   int? skip = 1,
   int? take = 25,
   decimal? amount = null,
   string searchCategory = null,
   string searchContact = null,
   string searchPaymentMethod = null,
   string searchType = null,
   string searchUser = null,
   DateTime? startDate = null,
   DateTime? endDate = null,
   SortField? sortBy = null,
   SortDirection? sortDirection = SortDirection.asc);



        Task<List<TransactionDto>> GetAllTransactionsRawByBookIdAsync(Guid bookId);
        Task<Guid> DuplicateTransactionToAnotherBookAsync(Guid transactionId, Guid targetBookId);



    }

}
