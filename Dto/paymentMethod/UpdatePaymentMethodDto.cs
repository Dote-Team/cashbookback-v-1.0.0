using System;
using System.Text.Json.Serialization;

namespace cashbook.Dto.paymentMethod;

public class UpdatePaymentMethodDto
{
	public string? Name { get; set; }

	[JsonIgnore]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
