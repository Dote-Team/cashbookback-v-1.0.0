using System;
using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using cashbook.Models.Enums;

namespace cashbook.Dto.transaction;

public class UpdateTransactionDto
{
	/// <summary>cash in أو cash out.</summary>
	public string? Type { get; set; }

	/// <summary>تاريخ القيد.</summary>
	public DateTime? Date { get; set; }

	/// <summary>الوصف.</summary>
	public string? Description { get; set; }

	/// <summary>المبلغ.</summary>
	public decimal? Amount { get; set; }

	/// <summary>IQD أو USD.</summary>
	public CurrencyCode? Currency { get; set; }

	/// <summary>سعر الصرف (للدولار).</summary>
	public decimal? ExchangeRate { get; set; }

	/// <summary>تاريخ سعر الصرف (للإيداع بالدولار).</summary>
	public DateTime? ExchangeDate { get; set; }

	/// <summary>التصنيف.</summary>
	public Guid? CategoryId { get; set; }

	/// <summary>طريقة الدفع.</summary>
	public Guid? PaymentMethodId { get; set; }

	/// <summary>جهة الاتصال.</summary>
	public Guid? ContactId { get; set; }

	/// <summary>قيم الحقول المخصصة (نص JSON). إرسالها يستبدل القيم السابقة.</summary>
	[DefaultValue("[{\"value\":\"string\",\"customFieldId\":\"0c244f1a-9955-43b9-6675-08dd99589a75\"}]")]
	public string? CustomFieldValues { get; set; }

	/// <summary>مرفقات جديدة تستبدل القديمة (التي تُؤرشف).</summary>
	public IFormFileCollection? Files { get; set; }
}
