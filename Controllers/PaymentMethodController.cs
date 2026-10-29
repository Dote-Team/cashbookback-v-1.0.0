using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.paymentMethod;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
public class PaymentMethodController : BusinessReferenceController<PaymentMethod, PaymentMethodDto, CreatePaymentMethodDto, UpdatePaymentMethodDto>
{
	private readonly IPaymentMethodRepository _repository;

	public PaymentMethodController(IPaymentMethodRepository repository, IPermissionService permissions, IMapper mapper)
		: base(repository, permissions, mapper)
	{
		_repository = repository;
	}

	protected override string ManagePermission => Permissions.PaymentMethodManage;

	protected override string EntityLabel => "طريقة الدفع";

	protected override Task<PaginatedResponse<PaymentMethodDto>> ListAsync(Guid businessId, int skip, int take, string? search) =>
		_repository.GetPaymentMethodsAsync(businessId, skip, take, search);

	protected override Guid BusinessIdOf(CreatePaymentMethodDto dto) => dto.BusinessId;
}
