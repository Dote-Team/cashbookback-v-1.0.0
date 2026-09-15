using System;

namespace cashbook.Dto.paymentMethod;

public class CreatePaymentMethodDto
{
	public string Name { get; set; }

	public Guid BusinessId { get; set; }
}
