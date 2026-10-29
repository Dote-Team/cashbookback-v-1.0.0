using System;
using System.Threading.Tasks;
using AutoMapper;
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

	public ContactController(IContactRepository repository, IPermissionService permissions, IMapper mapper)
		: base(repository, permissions, mapper)
	{
		_repository = repository;
	}

	protected override string ManagePermission => Permissions.ContactManage;

	protected override string EntityLabel => "جهة الاتصال";

	protected override Task<PaginatedResponse<ContactDto>> ListAsync(Guid businessId, int skip, int take, string? search) =>
		_repository.GetContactsAsync(businessId, skip, take, search);

	protected override Guid BusinessIdOf(CreateContactDto dto) => dto.BusinessId;
}
