using cashbook.Dto.category;
using cashbook.Dto;
using cashbook.Models;
using cashbook.Dto.paymentMethod;

namespace cashbook.Interfaces
{
    
        public interface IPaymentMethodRepository : IRepository<PaymentMethod>
        {
            Task<PaginatedResponse<PaymentMethodDto>> GetPaymentMethodsAsync(
        Guid businessId,
        int? skip = 1,
        int? take = 25,
        string search = null);
        }
    
}
