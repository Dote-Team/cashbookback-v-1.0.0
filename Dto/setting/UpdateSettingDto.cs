namespace cashbook.Dto.setting;

public class UpdateSettingDto
{
	/// <summary>true = التصنيف إلزامي عند إضافة قيد.</summary>
	public bool? CategoryStatus { get; set; }

	/// <summary>true = طريقة الدفع إلزامية.</summary>
	public bool? PaymentMethodStatus { get; set; }

	/// <summary>true = جهة الاتصال إلزامية.</summary>
	public bool? ContactStatus { get; set; }
}
