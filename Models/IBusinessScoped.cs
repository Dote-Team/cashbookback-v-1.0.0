using System;

namespace cashbook.Models;

/// <summary>بيانات مرجعية مشتركة على مستوى المنشأة (تصنيفات، جهات اتصال، طرق دفع).</summary>
public interface IBusinessScoped
{
	Guid Id { get; set; }

	Guid BusinessId { get; set; }

	DateTime UpdatedAt { get; set; }
}
