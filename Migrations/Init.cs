using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;
using cashbook.Data;

namespace cashbook.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907183736_Init")]
public class Init : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("Businesses", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Name = table.Column<string>("nvarchar(max)"),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_Businesses", x => x.Id);
		});
		migrationBuilder.CreateTable("Users", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Name = table.Column<string>("nvarchar(max)"),
			Email = table.Column<string>("nvarchar(max)"),
			Password = table.Column<string>("nvarchar(max)"),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2"),
			ProfileImage = table.Column<string>("nvarchar(max)", null, null, rowVersion: false, null, nullable: true)
		}, null, table =>
		{
			table.PrimaryKey("PK_Users", x => x.Id);
		});
		migrationBuilder.CreateTable("Books", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Name = table.Column<string>("nvarchar(max)"),
			BusinessId = table.Column<Guid>("uniqueidentifier"),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_Books", x => x.Id);
			table.ForeignKey("FK_Books_Businesses_BusinessId", x => x.BusinessId, "Businesses", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("Categories", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Name = table.Column<string>("nvarchar(max)"),
			BusinessId = table.Column<Guid>("uniqueidentifier"),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_Categories", x => x.Id);
			table.ForeignKey("FK_Categories_Businesses_BusinessId", x => x.BusinessId, "Businesses", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("Contacts", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Name = table.Column<string>("nvarchar(max)"),
			Phone = table.Column<string>("nvarchar(max)"),
			BusinessId = table.Column<Guid>("uniqueidentifier"),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_Contacts", x => x.Id);
			table.ForeignKey("FK_Contacts_Businesses_BusinessId", x => x.BusinessId, "Businesses", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("PaymentMethods", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Name = table.Column<string>("nvarchar(max)"),
			BusinessId = table.Column<Guid>("uniqueidentifier"),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_PaymentMethods", x => x.Id);
			table.ForeignKey("FK_PaymentMethods_Businesses_BusinessId", x => x.BusinessId, "Businesses", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("BusinessUsers", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			UserId = table.Column<Guid>("uniqueidentifier"),
			BusinessId = table.Column<Guid>("uniqueidentifier"),
			BookIds = table.Column<string>("nvarchar(max)", null, null, rowVersion: false, null, nullable: true),
			Role = table.Column<string>("nvarchar(max)")
		}, null, table =>
		{
			table.PrimaryKey("PK_BusinessUsers", x => x.Id);
			table.ForeignKey("FK_BusinessUsers_Businesses_BusinessId", x => x.BusinessId, "Businesses", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_BusinessUsers_Users_UserId", x => x.UserId, "Users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("Sessions", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			RefreshToken = table.Column<string>("nvarchar(max)"),
			DeviceToken = table.Column<string>("nvarchar(max)"),
			UserId = table.Column<Guid>("uniqueidentifier"),
			ExpiredAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_Sessions", x => x.Id);
			table.ForeignKey("FK_Sessions_Users_UserId", x => x.UserId, "Users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("CustomFields", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Key = table.Column<string>("nvarchar(max)"),
			BookId = table.Column<Guid>("uniqueidentifier"),
			IsRequired = table.Column<bool>("bit")
		}, null, table =>
		{
			table.PrimaryKey("PK_CustomFields", x => x.Id);
			table.ForeignKey("FK_CustomFields_Books_BookId", x => x.BookId, "Books", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("Settings", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			CategoryStatus = table.Column<bool>("bit"),
			PaymentMethodStatus = table.Column<bool>("bit"),
			ContactStatus = table.Column<bool>("bit"),
			BookId = table.Column<Guid>("uniqueidentifier"),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_Settings", x => x.Id);
			table.ForeignKey("FK_Settings_Books_BookId", x => x.BookId, "Books", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
		});
		migrationBuilder.CreateTable("Transactions", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Type = table.Column<string>("nvarchar(max)"),
			Date = table.Column<DateTime>("datetime2"),
			Description = table.Column<string>("nvarchar(max)", null, null, rowVersion: false, null, nullable: true),
			Amount = table.Column<decimal>("decimal(18,2)"),
			CategoryId = table.Column<Guid>("uniqueidentifier", null, null, rowVersion: false, null, nullable: true),
			PaymentMethodId = table.Column<Guid>("uniqueidentifier", null, null, rowVersion: false, null, nullable: true),
			BookId = table.Column<Guid>("uniqueidentifier"),
			UserId = table.Column<Guid>("uniqueidentifier"),
			ContactId = table.Column<Guid>("uniqueidentifier", null, null, rowVersion: false, null, nullable: true),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_Transactions", x => x.Id);
			table.ForeignKey("FK_Transactions_Books_BookId", x => x.BookId, "Books", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_Transactions_Categories_CategoryId", x => x.CategoryId, "Categories", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_Transactions_Contacts_ContactId", x => x.ContactId, "Contacts", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_Transactions_PaymentMethods_PaymentMethodId", x => x.PaymentMethodId, "PaymentMethods", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_Transactions_Users_UserId", x => x.UserId, "Users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("Attachements", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Files = table.Column<string>("nvarchar(max)"),
			TransactionId = table.Column<Guid>("uniqueidentifier"),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_Attachements", x => x.Id);
			table.ForeignKey("FK_Attachements_Transactions_TransactionId", x => x.TransactionId, "Transactions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("CustomFieldValues", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Value = table.Column<string>("nvarchar(max)"),
			TransactionId = table.Column<Guid>("uniqueidentifier"),
			CustomFieldId = table.Column<Guid>("uniqueidentifier")
		}, null, table =>
		{
			table.PrimaryKey("PK_CustomFieldValues", x => x.Id);
			table.ForeignKey("FK_CustomFieldValues_CustomFields_CustomFieldId", x => x.CustomFieldId, "CustomFields", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_CustomFieldValues_Transactions_TransactionId", x => x.TransactionId, "Transactions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("TransactionHistories", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uniqueidentifier"),
			Operation = table.Column<string>("nvarchar(max)"),
			Description = table.Column<string>("nvarchar(max)", null, null, rowVersion: false, null, nullable: true),
			Type = table.Column<string>("nvarchar(max)"),
			From = table.Column<decimal>("decimal(18,2)", null, null, rowVersion: false, null, nullable: true),
			To = table.Column<decimal>("decimal(18,2)", null, null, rowVersion: false, null, nullable: true),
			Amount = table.Column<decimal>("decimal(18,2)"),
			BookId = table.Column<Guid>("uniqueidentifier"),
			TransactionId = table.Column<Guid>("uniqueidentifier", null, null, rowVersion: false, null, nullable: true),
			UserId = table.Column<Guid>("uniqueidentifier"),
			CreatedAt = table.Column<DateTime>("datetime2"),
			UpdatedAt = table.Column<DateTime>("datetime2")
		}, null, table =>
		{
			table.PrimaryKey("PK_TransactionHistories", x => x.Id);
			table.ForeignKey("FK_TransactionHistories_Books_BookId", x => x.BookId, "Books", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_TransactionHistories_Transactions_TransactionId", x => x.TransactionId, "Transactions", "Id", null, ReferentialAction.NoAction, ReferentialAction.SetNull);
			table.ForeignKey("FK_TransactionHistories_Users_UserId", x => x.UserId, "Users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateIndex("IX_Attachements_TransactionId", "Attachements", "TransactionId");
		migrationBuilder.CreateIndex("IX_Books_BusinessId", "Books", "BusinessId");
		migrationBuilder.CreateIndex("IX_BusinessUsers_BusinessId", "BusinessUsers", "BusinessId");
		migrationBuilder.CreateIndex("IX_BusinessUsers_UserId", "BusinessUsers", "UserId");
		migrationBuilder.CreateIndex("IX_Categories_BusinessId", "Categories", "BusinessId");
		migrationBuilder.CreateIndex("IX_Contacts_BusinessId", "Contacts", "BusinessId");
		migrationBuilder.CreateIndex("IX_CustomFields_BookId", "CustomFields", "BookId");
		migrationBuilder.CreateIndex("IX_CustomFieldValues_CustomFieldId", "CustomFieldValues", "CustomFieldId", null, unique: true);
		migrationBuilder.CreateIndex("IX_CustomFieldValues_TransactionId", "CustomFieldValues", "TransactionId");
		migrationBuilder.CreateIndex("IX_PaymentMethods_BusinessId", "PaymentMethods", "BusinessId");
		migrationBuilder.CreateIndex("IX_Sessions_UserId", "Sessions", "UserId");
		migrationBuilder.CreateIndex("IX_Settings_BookId", "Settings", "BookId");
		migrationBuilder.CreateIndex("IX_TransactionHistories_BookId", "TransactionHistories", "BookId");
		migrationBuilder.CreateIndex("IX_TransactionHistories_TransactionId", "TransactionHistories", "TransactionId");
		migrationBuilder.CreateIndex("IX_TransactionHistories_UserId", "TransactionHistories", "UserId");
		migrationBuilder.CreateIndex("IX_Transactions_BookId", "Transactions", "BookId");
		migrationBuilder.CreateIndex("IX_Transactions_CategoryId", "Transactions", "CategoryId");
		migrationBuilder.CreateIndex("IX_Transactions_ContactId", "Transactions", "ContactId");
		migrationBuilder.CreateIndex("IX_Transactions_PaymentMethodId", "Transactions", "PaymentMethodId");
		migrationBuilder.CreateIndex("IX_Transactions_UserId", "Transactions", "UserId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("Attachements");
		migrationBuilder.DropTable("BusinessUsers");
		migrationBuilder.DropTable("CustomFieldValues");
		migrationBuilder.DropTable("Sessions");
		migrationBuilder.DropTable("Settings");
		migrationBuilder.DropTable("TransactionHistories");
		migrationBuilder.DropTable("CustomFields");
		migrationBuilder.DropTable("Transactions");
		migrationBuilder.DropTable("Books");
		migrationBuilder.DropTable("Categories");
		migrationBuilder.DropTable("Contacts");
		migrationBuilder.DropTable("PaymentMethods");
		migrationBuilder.DropTable("Users");
		migrationBuilder.DropTable("Businesses");
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
			b.Property<decimal>("Amount").HasColumnType("decimal(18,2)");
			b.Property<Guid>("BookId").HasColumnType("uniqueidentifier");
			b.Property<Guid?>("CategoryId").HasColumnType("uniqueidentifier");
			b.Property<Guid?>("ContactId").HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<DateTime>("Date").HasColumnType("datetime2");
			b.Property<string>("Description").HasColumnType("nvarchar(max)");
			b.Property<Guid?>("PaymentMethodId").HasColumnType("uniqueidentifier");
			b.Property<string>("Type").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
			b.Property<Guid>("UserId").HasColumnType("uniqueidentifier");
			b.HasKey("Id");
			b.HasIndex("BookId");
			b.HasIndex("CategoryId");
			b.HasIndex("ContactId");
			b.HasIndex("PaymentMethodId");
			b.HasIndex("UserId");
			b.ToTable("Transactions");
		});
		modelBuilder.Entity("cashbook.Models.TransactionHistory", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
			b.Property<decimal>("Amount").HasColumnType("decimal(18,2)");
			b.Property<Guid>("BookId").HasColumnType("uniqueidentifier");
			b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
			b.Property<string>("Description").HasColumnType("nvarchar(max)");
			b.Property<decimal?>("From").HasColumnType("decimal(18,2)");
			b.Property<string>("Operation").IsRequired().HasColumnType("nvarchar(max)");
			b.Property<decimal?>("To").HasColumnType("decimal(18,2)");
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
