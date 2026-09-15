using System.ComponentModel;

namespace cashbook.Dto.transaction

{
    public class CreateTransactionDto
    {
        public string Type { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? PaymentMethodId { get; set; }
        public Guid? ContactId { get; set; }
        public Guid BookId { get; set; }
        [DefaultValue("[{\"value\":\"string\",\"customFieldId\":\"0c244f1a-9955-43b9-6675-08dd99589a75\"}]")]

        public string? CustomFieldValues { get; set; }
        public IFormFileCollection? Files { get; set; }
    }

    public class CustomFieldValueCreateDto
    {
        public string Value { get; set; }
        public Guid CustomFieldId { get; set; }
    }


}
