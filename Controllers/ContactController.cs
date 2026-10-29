using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.contact;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
public class ContactController : BusinessReferenceController<Contact, ContactDto, CreateContactDto, UpdateContactDto>
{
	private readonly IContactRepository _repository;

	public ContactController(IContactRepository repository, IPermissionService permissions)
		: base(repository, permissions)
	{
		_repository = repository;
	}

	protected override string ManagePermission => Permissions.ContactManage;

	protected override string EntityLabel => "جهة الاتصال";

	protected override Task<PaginatedResponse<ContactDto>> ListAsync(Guid businessId, int skip, int take, string? search) =>
		_repository.GetContactsAsync(businessId, skip, take, search);

	protected override Guid BusinessIdOf(CreateContactDto dto) => dto.BusinessId;

	protected override ContactDto ToDto(Contact entity) => new ContactDto
	{
		Id = entity.Id,
		Name = entity.Name,
		Phone = entity.Phone,
		BusinessId = entity.BusinessId,
		CreatedAt = entity.CreatedAt,
		UpdatedAt = entity.UpdatedAt
	};

	protected override string? ValidateCreate(CreateContactDto dto) => string.IsNullOrWhiteSpace(dto.Name) ? "الاسم مطلوب." : null;

	protected override Contact FromCreate(CreateContactDto dto) => new Contact
	{
		Name = dto.Name.Trim(),
		Phone = dto.Phone?.Trim() ?? string.Empty,
		BusinessId = dto.BusinessId
	};

	protected override void ApplyUpdate(UpdateContactDto dto, Contact entity)
	{
		if (!string.IsNullOrWhiteSpace(dto.Name))
		{
			entity.Name = dto.Name.Trim();
		}
		// الهاتف يُقبل فارغاً لمسحه، لكن غيابه من الطلب (null) لا يغيّره.
		if (dto.Phone != null)
		{
			entity.Phone = dto.Phone.Trim();
		}
	}
}
