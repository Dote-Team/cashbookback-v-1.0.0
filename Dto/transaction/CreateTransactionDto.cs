using System;
using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using cashbook.Models.Enums;

namespace cashbook.Dto.transaction;

public class CreateTransactionDto
{
	/// <summary>cash in (إيداع) أو cash out (سحب).</summary>
	public string Type { get; set; }

	/// <summary>تاريخ القيد.</summary>
	public DateTime Date { get; set; }

	/// <summary>الوصف.</summary>
	public string? Description { get; set; }

	/// <summary>المبلغ (موجب دائماً، والاتجاه من Type).</summary>
	public decimal Amount { get; set; }

	/// <summary>IQD أو USD.</summary>
	[DefaultValue(CurrencyCode.IQD)]
	public CurrencyCode Currency { get; set; } = CurrencyCode.IQD;

	/// <summary>سعر الصرف: إلزامي للدولار.</summary>
	public decimal? ExchangeRate { get; set; }

	/// <summary>تاريخ سعر الصرف: للإيداع بالدولار فقط.</summary>
	public DateTime? ExchangeDate { get; set; }

	/// <summary>التصنيف.</summary>
	public Guid? CategoryId { get; set; }

	/// <summary>طريقة الدفع.</summary>
	public Guid? PaymentMethodId { get; set; }

	/// <summary>جهة الاتصال.</summary>
	public Guid? ContactId { get; set; }

	/// <summary>الخزنة.</summary>
	public Guid BookId { get; set; }

	/// <summary>قيم الحقول المخصصة: نص JSON، مثل [{"customFieldId":"...","value":"123"}].</summary>
	[DefaultValue("[{\"value\":\"string\",\"customFieldId\":\"0c244f1a-9955-43b9-6675-08dd99589a75\"}]")]
	public string? CustomFieldValues { get; set; }

	/// <summary>مرفقات: حتى 5 ملفات، كلٌّ حتى 10MB، صور أو PDF.</summary>
	public IFormFileCollection? Files { get; set; }
}
