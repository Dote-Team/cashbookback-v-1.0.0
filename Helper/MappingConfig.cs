using AutoMapper;
using cashbook.Dto.book;
using cashbook.Dto.business;
using cashbook.Dto.category;
using cashbook.Dto.contact;
using cashbook.Dto.user;
using cashbook.Dto.customfield;
using cashbook.Models;
using cashbook.Dto.transaction;
using cashbook.Dto.businessUser;
using cashbook.Dto.paymentMethod;
using cashbook.Dto.transactionHistory;




namespace cashbook.Helper
{
    public class MappingConfig : Profile
    {
        public MappingConfig()
        {
            CreateMap<User, UserDto>().ReverseMap();
            CreateMap<User, RegisterationRequestDto>().ReverseMap();
            CreateMap<User, UserUpdateDto>().ReverseMap();
            CreateMap<User, LoginRequestDto>().ReverseMap();
            CreateMap<User, LoginResponseDto>().ReverseMap();

            CreateMap<Book, BookDto>().ReverseMap();
            CreateMap<Book, CreateBookDto>().ReverseMap();
            CreateMap<Book, UpdateBookDto>().ReverseMap();

            CreateMap<Business, BusinessDto>().ReverseMap();
            CreateMap<Business, CreateBusinessDto>().ReverseMap();
            CreateMap<Business, UpdateBusinessDto>().ReverseMap();

            CreateMap<Contact, ContactDto>().ReverseMap();
            CreateMap<Contact, CreateContactDto>().ReverseMap();
            CreateMap<Contact, UpdateContactDto>().ReverseMap();

            CreateMap<Category, CategoryDto>().ReverseMap();
            CreateMap<Category, CreateCategoryDto>().ReverseMap();
            CreateMap<Category, UpdateCategoryDto>().ReverseMap();

            CreateMap<PaymentMethod, PaymentMethodDto>().ReverseMap();
            CreateMap<PaymentMethod, CreatePaymentMethodDto>().ReverseMap();
            CreateMap<PaymentMethod, UpdatePaymentMethodDto>().ReverseMap();

            CreateMap<CustomField, CustomFieldDto>().ReverseMap();
            CreateMap<CustomField, CreateCustomFieldDto>().ReverseMap();
            CreateMap<CustomField, UpdateCustomFieldDto>().ReverseMap();

            CreateMap<Transaction, UpdateTransactionDto>().ReverseMap();
            CreateMap<Transaction, CreateTransactionDto>().ReverseMap();

            CreateMap<BusinessUser, UpdateBusinessUserDto>().ReverseMap();
            CreateMap<BusinessUser, BusinessUserDto>().ReverseMap();

            CreateMap<TransactionHistory, TransactionHistoryDto>().ReverseMap();





        }
    }
}
