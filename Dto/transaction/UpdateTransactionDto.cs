using System;
using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using cashbook.Models.Enums;

namespace cashbook.Dto.transaction;

public class UpdateTransactionDto
{
	public string? Type { get; set; }

	public DateTime? Date { get; set; }

	public string? Description { get; set; }

	public decimal? Amount { get; set; }

	public CurrencyCode? Currency { get; set; }

	public decimal? ExchangeRate { get; set; }

	public DateTime? ExchangeDate { get; set; }

	public Guid? CategoryId { get; set; }

	public Guid? PaymentMethodId { get; set; }

	public Guid? ContactId { get; set; }

	[DefaultValue("[{\"value\":\"string\",\"customFieldId\":\"0c244f1a-9955-43b9-6675-08dd99589a75\"}]")]
	public string? CustomFieldValues { get; set; }

	public IFormFileCollection? Files { get; set; }
}
