

using cashbook.Dto.category;
using cashbook.Dto.contact;
using cashbook.Dto.paymentMethod;
using cashbook.Dto.setting;
using cashbook.Dto.user;

namespace cashbook.Dto.Book
{
    public class BookDetailsDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid BusinessId { get; set; }
        public decimal CashInTotal { get; set; }
        public decimal CashOutTotal { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public SettingDto? Setting { get; set; }


    }





    public class CustomFieldValueDto
    {
        public Guid Id { get; set; }
        public string Value { get; set; }
        public Guid CustomFieldId { get; set; }
        public Guid TransactionId { get; set; }
        public CustomFieldDto? CustomFields { get; set; }

    }

    public class CustomFieldDto
    {
        public Guid Id { get; set; }
        public string Key { get; set; }
        public Guid BookId { get; set; }
        public bool IsRequired { get; set; }
    }

    public class TransactionDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid UserId { get; set; }
        public CategoryDto? Category { get; set; }
        public PaymentMethodDto? PaymentMethod { get; set; }
        public ContactDto? Contact { get; set; }
        public UserDto? User { get; set; }
        public Guid BookId { get; set; }
        public Guid CustomFieldId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public decimal NewBalance { get; set; }

        public List<CustomFieldValueDto>? CustomFieldValues { get; set; }
        public List<AttachmentDto> Attachments { get; set; }
    }


    public class AttachmentDto
    {
        public Guid Id { get; set; }
        public string Files { get; set; }
        public Guid TransactionId { get; set; }
    }

    public enum SortField
    {
        Category,
        Description,
        Amount,
        NewBalance,
        Date,
        PaymentMethod,
        CreatedAt,
        Name,
        Balance
    }

    public enum SortDirection
    {
        asc,
        desc
    }

}
