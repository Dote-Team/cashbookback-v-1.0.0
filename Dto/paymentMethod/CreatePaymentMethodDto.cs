using System;

namespace cashbook.Dto.paymentMethod;

public class CreatePaymentMethodDto
{
	/// <summary>اسم طريقة الدفع.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>المنشأة.</summary>
	public Guid BusinessId { get; set; }
}
