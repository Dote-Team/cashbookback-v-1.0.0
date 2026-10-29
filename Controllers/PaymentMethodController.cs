using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.paymentMethod;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>طرق الدفع على مستوى المنشأة. العرض لكل عضو، والإدارة لمن يملك payment_method.manage.</summary>
[Route("API/[Controller]")]
public class PaymentMethodController : BusinessReferenceController<PaymentMethod, PaymentMethodDto, CreatePaymentMethodDto, UpdatePaymentMethodDto>
{
	private readonly IPaymentMethodRepository _repository;

	public PaymentMethodController(IPaymentMethodRepository repository, IPermissionService permissions)
		: base(repository, permissions)
	{
		_repository = repository;
	}

	protected override string ManagePermission => Permissions.PaymentMethodManage;

	protected override string EntityLabel => "طريقة الدفع";

	protected override Task<PaginatedResponse<PaymentMethodDto>> ListAsync(Guid businessId, int skip, int take, string? search) =>
		_repository.GetPaymentMethodsAsync(businessId, skip, take, search);

	protected override Guid BusinessIdOf(CreatePaymentMethodDto dto) => dto.BusinessId;

	protected override PaymentMethodDto ToDto(PaymentMethod entity) => new PaymentMethodDto
	{
		Id = entity.Id,
		Name = entity.Name,
		BusinessId = entity.BusinessId,
		CreatedAt = entity.CreatedAt,
		UpdatedAt = entity.UpdatedAt
	};

	protected override string? ValidateCreate(CreatePaymentMethodDto dto) => string.IsNullOrWhiteSpace(dto.Name) ? "الاسم مطلوب." : null;

	protected override PaymentMethod FromCreate(CreatePaymentMethodDto dto) => new PaymentMethod { Name = dto.Name.Trim(), BusinessId = dto.BusinessId };

	protected override void ApplyUpdate(UpdatePaymentMethodDto dto, PaymentMethod entity)
	{
		if (!string.IsNullOrWhiteSpace(dto.Name))
		{
			entity.Name = dto.Name.Trim();
		}
	}
}
