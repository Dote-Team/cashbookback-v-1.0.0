using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using cashbook.Dto.transactionHistory;
using cashbook.Models;

namespace cashbook.Interfaces;

public interface ITransactionHistoryRepository : IRepository<TransactionHistory>
{
	Task<List<TransactionHistoryDto>> GetAllByBookIdAsync(Guid bookId);

	Task<List<TransactionHistoryDto>> GetAllByTransactionIdAsync(Guid transactionId);
}
