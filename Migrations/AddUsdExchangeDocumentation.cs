using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using cashbook.Data;

namespace cashbook.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260915122452_AddUsdExchangeDocumentation")]
public class AddUsdExchangeDocumentation : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<DateTime>("ExchangeDate", "Transactions", "datetime2", null, null, rowVersion: false, null, nullable: true);
		int? precision = 18;
		int? scale = 6;
		migrationBuilder.AddColumn<decimal>("ExchangeRate", "Transactions", "decimal(18,6)", null, null, rowVersion: false, null, nullable: true, null, null, null, null, null, null, precision, scale);
		migrationBuilder.AddColumn<DateTime>("ExchangeDate", "TransactionHistories", "datetime2", null, null, rowVersion: false, null, nullable: true);
		scale = 18;
		precision = 6;
		migrationBuilder.AddColumn<decimal>("ExchangeRate", "TransactionHistories", "decimal(18,6)", null, null, rowVersion: false, null, nullable: true, null, null, null, null, null, null, scale, precision);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn("ExchangeDate", "Transactions");
		migrationBuilder.DropColumn("ExchangeRate", "Transactions");
		migrationBuilder.DropColumn("ExchangeDate", "TransactionHistories");
		migrationBuilder.DropColumn("ExchangeRate", "TransactionHistories");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "9.0.5").HasAnnotation("Relational:MaxIdentifierLength", 128);
		modelBuilder.UseIdentityColumns(1L);
		modelBuilder.Entity("cashbook.Models.Attachement", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Files").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<Guid>("TransactionId").HasColumnType("uniqueidentifier");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.HasKey("Id");
			b.HasIndex("TransactionId");
			b.ToTable("Attachements");
		});
		modelBuilder.Entity("cashbook.Models.Book", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<Guid>("BusinessId").HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Name").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.HasKey("Id");
			b.HasIndex("BusinessId");
			b.ToTable("Books");
		});
		modelBuilder.Entity("cashbook.Models.Business", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Name").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.HasKey("Id");
			b.ToTable("Businesses");
		});
		modelBuilder.Entity("cashbook.Models.BusinessUser", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.PrimitiveCollection<string>("BookIds").HasColumnType("nvarchar(max)");
			b.Property<Guid>("BusinessId").HasColumnType("uniqueidentifier");
			b.Property<string>("Role").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<Guid>("UserId").HasColumnType("uniqueidentifier");
			b.HasKey("Id");
			b.HasIndex("BusinessId");
			b.HasIndex("UserId");
			b.ToTable("BusinessUsers");
		});
		modelBuilder.Entity("cashbook.Models.Category", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<Guid>("BusinessId").HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Name").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.HasKey("Id");
			b.HasIndex("BusinessId");
			b.ToTable("Categories");
		});
		modelBuilder.Entity("cashbook.Models.Contact", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<Guid>("BusinessId").HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Name").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<string>("Phone").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.HasKey("Id");
			b.HasIndex("BusinessId");
			b.ToTable("Contacts");
		});
		modelBuilder.Entity("cashbook.Models.CustomField", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<Guid>("BookId").HasColumnType("uniqueidentifier");
			b.Property<bool>("IsRequired").HasColumnType("bit");
			b.Property<string>("Key").IsRequired().HasColumnType("nvarchar(max)");
			b.HasKey("Id");
			b.HasIndex("BookId");
			b.ToTable("CustomFields");
		});
		modelBuilder.Entity("cashbook.Models.CustomFieldValue", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<Guid>("CustomFieldId").HasColumnType("uniqueidentifier");
			b.Property<Guid>("TransactionId").HasColumnType("uniqueidentifier");
			b.Property<string>("Value").IsRequired().HasColumnType("nvarchar(max)");
			b.HasKey("Id");
			b.HasIndex("CustomFieldId").IsUnique();
			b.HasIndex("TransactionId");
			b.ToTable("CustomFieldValues");
		});
		modelBuilder.Entity("cashbook.Models.PaymentMethod", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<Guid>("BusinessId").HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Name").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.HasKey("Id");
			b.HasIndex("BusinessId");
			b.ToTable("PaymentMethods");
		});
		modelBuilder.Entity("cashbook.Models.Session", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<string>("DeviceToken").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<DateTime>("ExpiredAt").HasColumnType("datetime2");
			b.Property<string>("RefreshToken").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<Guid>("UserId").HasColumnType("uniqueidentifier");
			b.HasKey("Id");
			b.HasIndex("UserId");
			b.ToTable("Sessions");
		});
		modelBuilder.Entity("cashbook.Models.Setting", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<Guid>("BookId").HasColumnType("uniqueidentifier");
			b.Property<bool>("CategoryStatus").HasColumnType("bit");
			b.Property<bool>("ContactStatus").HasColumnType("bit");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<bool>("PaymentMethodStatus").HasColumnType("bit");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.HasKey("Id");
			b.HasIndex("BookId");
			b.ToTable("Settings");
		});
		modelBuilder.Entity("cashbook.Models.Transaction", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<decimal>("Amount").HasPrecision(18, 3).HasColumnType("decimal(18,3)");
			b.Property<Guid>("BookId").HasColumnType("uniqueidentifier");
			b.Property<Guid?>("CategoryId").HasColumnType("uniqueidentifier");
			b.Property<Guid?>("ContactId").HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Currency").IsRequired().HasMaxLength(3)
				.HasColumnType("nvarchar(3)");
			b.Property<DateTime>("Date").HasColumnType("datetime2");
			b.Property<string>("Description").HasColumnType("nvarchar(max)");
			b.Property<DateTime?>("ExchangeDate").HasColumnType("datetime2");
			b.Property<decimal?>("ExchangeRate").HasPrecision(18, 6).HasColumnType("decimal(18,6)");
			b.Property<Guid?>("PaymentMethodId").HasColumnType("uniqueidentifier");
			b.Property<string>("Type").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.Property<Guid>("UserId").HasColumnType("uniqueidentifier");
			b.HasKey("Id");
			b.HasIndex("CategoryId");
			b.HasIndex("ContactId");
			b.HasIndex("PaymentMethodId");
			b.HasIndex("UserId");
			b.HasIndex("BookId", "Currency");
			b.ToTable("Transactions");
		});
		modelBuilder.Entity("cashbook.Models.TransactionHistory", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<decimal>("Amount").HasPrecision(18, 3).HasColumnType("decimal(18,3)");
			b.Property<Guid>("BookId").HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Description").HasColumnType("nvarchar(max)");
			b.Property<DateTime?>("ExchangeDate").HasColumnType("datetime2");
			b.Property<decimal?>("ExchangeRate").HasPrecision(18, 6).HasColumnType("decimal(18,6)");
			b.Property<decimal?>("From").HasPrecision(18, 3).HasColumnType("decimal(18,3)");
			b.Property<string>("Operation").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<decimal?>("To").HasPrecision(18, 3).HasColumnType("decimal(18,3)");
			b.Property<Guid?>("TransactionId").HasColumnType("uniqueidentifier");
			b.Property<string>("Type").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.Property<Guid>("UserId").HasColumnType("uniqueidentifier");
			b.HasKey("Id");
			b.HasIndex("BookId");
			b.HasIndex("TransactionId");
			b.HasIndex("UserId");
			b.ToTable("TransactionHistories");
		});
		modelBuilder.Entity("cashbook.Models.User", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Email").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<string>("Name").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<string>("Password").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<string>("ProfileImage").HasColumnType("nvarchar(max)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.HasKey("Id");
			b.ToTable("Users");
		});
		modelBuilder.Entity("cashbook.Models.Attachement", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Transaction", "Transaction").WithMany("Attachements").HasForeignKey("TransactionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Transaction");
		});
		modelBuilder.Entity("cashbook.Models.Book", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Business", "Business").WithMany("Books").HasForeignKey("BusinessId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Business");
		});
		modelBuilder.Entity("cashbook.Models.BusinessUser", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Business", "Business").WithMany("BusinessUsers").HasForeignKey("BusinessId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("cashbook.Models.User", "User").WithMany("BusinessUsers").HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Business");
			b.Navigation("User");
		});
		modelBuilder.Entity("cashbook.Models.Category", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Business", "Business").WithMany("Categories").HasForeignKey("BusinessId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Business");
		});
		modelBuilder.Entity("cashbook.Models.Contact", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Business", "Business").WithMany("Contacts").HasForeignKey("BusinessId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Business");
		});
		modelBuilder.Entity("cashbook.Models.CustomField", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Book", "Book").WithMany("CustomFields").HasForeignKey("BookId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Book");
		});
		modelBuilder.Entity("cashbook.Models.CustomFieldValue", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.CustomField", "CustomField").WithOne("CustomFieldValues").HasForeignKey("cashbook.Models.CustomFieldValue", "CustomFieldId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("cashbook.Models.Transaction", "Transaction").WithMany("CustomFieldValues").HasForeignKey("TransactionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("CustomField");
			b.Navigation("Transaction");
		});
		modelBuilder.Entity("cashbook.Models.PaymentMethod", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Business", "Business").WithMany("PaymentMethods").HasForeignKey("BusinessId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Business");
		});
		modelBuilder.Entity("cashbook.Models.Session", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.User", "User").WithMany("Sessions").HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("User");
		});
		modelBuilder.Entity("cashbook.Models.Setting", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Book", "Book").WithMany("Settings").HasForeignKey("BookId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Book");
		});
		modelBuilder.Entity("cashbook.Models.Transaction", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Book", "Book").WithMany("Transactions").HasForeignKey("BookId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("cashbook.Models.Category", "Category").WithMany("Transactions").HasForeignKey("CategoryId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("cashbook.Models.Contact", "Contact").WithMany("Transactions").HasForeignKey("ContactId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("cashbook.Models.PaymentMethod", "PaymentMethod").WithMany("Transactions").HasForeignKey("PaymentMethodId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("cashbook.Models.User", "User").WithMany("Transactions").HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Book");
			b.Navigation("Category");
			b.Navigation("Contact");
			b.Navigation("PaymentMethod");
			b.Navigation("User");
		});
		modelBuilder.Entity("cashbook.Models.TransactionHistory", delegate(EntityTypeBuilder b)
		{
			b.HasOne("cashbook.Models.Book", "Book").WithMany("TransactionHistories").HasForeignKey("BookId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("cashbook.Models.Transaction", "Transaction").WithMany("TransactionHistories").HasForeignKey("TransactionId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("cashbook.Models.User", "User").WithMany("TransactionHistories").HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Book");
			b.Navigation("Transaction");
			b.Navigation("User");
		});
		modelBuilder.Entity("cashbook.Models.Book", delegate(EntityTypeBuilder b)
		{
			b.Navigation("CustomFields");
			b.Navigation("Settings");
			b.Navigation("TransactionHistories");
			b.Navigation("Transactions");
		});
		modelBuilder.Entity("cashbook.Models.Business", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Books");
			b.Navigation("BusinessUsers");
			b.Navigation("Categories");
			b.Navigation("Contacts");
			b.Navigation("PaymentMethods");
		});
		modelBuilder.Entity("cashbook.Models.Category", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Transactions");
		});
		modelBuilder.Entity("cashbook.Models.Contact", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Transactions");
		});
		modelBuilder.Entity("cashbook.Models.CustomField", delegate(EntityTypeBuilder b)
		{
			b.Navigation("CustomFieldValues").IsRequired();
		});
		modelBuilder.Entity("cashbook.Models.PaymentMethod", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Transactions");
		});
		modelBuilder.Entity("cashbook.Models.Transaction", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Attachements");
			b.Navigation("CustomFieldValues");
			b.Navigation("TransactionHistories");
		});
		modelBuilder.Entity("cashbook.Models.User", delegate(EntityTypeBuilder b)
		{
			b.Navigation("BusinessUsers");
			b.Navigation("Sessions");
			b.Navigation("TransactionHistories");
			b.Navigation("Transactions");
		});
	}
}
