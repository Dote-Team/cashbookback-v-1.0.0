using System;
using System.Threading.Tasks;
using cashbook.Dto;
using cashbook.Dto.paymentMethod;
using cashbook.Models;

namespace cashbook.Interfaces;

public interface IPaymentMethodRepository : IRepository<PaymentMethod>
{
	Task<PaginatedResponse<PaymentMethodDto>> GetPaymentMethodsAsync(Guid businessId, int? skip = 1, int? take = 25, string search = null);
}
