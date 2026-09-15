using System;
using System.Collections.Generic;
using cashbook.Dto.category;
using cashbook.Dto.contact;
using cashbook.Dto.paymentMethod;
using cashbook.Dto.user;
using cashbook.Models.Enums;

namespace cashbook.Dto.Book;

public class TransactionDto
{
	public Guid Id { get; set; }

	public string Type { get; set; }

	public DateTime Date { get; set; }

	public string? Description { get; set; }

	public decimal Amount { get; set; }

	public CurrencyCode Currency { get; set; }

	public decimal? ExchangeRate { get; set; }

	public DateTime? ExchangeDate { get; set; }

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

	public decimal NewBalanceIqd { get; set; }

	public decimal NewBalanceUsd { get; set; }

	public List<CustomFieldValueDto>? CustomFieldValues { get; set; }

	public List<AttachmentDto> Attachments { get; set; }
}
