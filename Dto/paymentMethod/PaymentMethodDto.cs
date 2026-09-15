using System;

namespace cashbook.Dto.paymentMethod;

public class PaymentMethodDto
{
	public Guid Id { get; set; }

	public string Name { get; set; }

	public Guid BusinessId { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
