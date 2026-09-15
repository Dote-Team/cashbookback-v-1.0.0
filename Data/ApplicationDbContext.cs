using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cashbook.Models;

namespace cashbook.Data;

public class ApplicationDbContext : DbContext
{
	public DbSet<User> Users { get; set; }

	public DbSet<Category> Categories { get; set; }

	public DbSet<PaymentMethod> PaymentMethods { get; set; }

	public DbSet<Session> Sessions { get; set; }

	public DbSet<BusinessUser> BusinessUsers { get; set; }

	public DbSet<Business> Businesses { get; set; }

	public DbSet<Book> Books { get; set; }

	public DbSet<Contact> Contacts { get; set; }

	public DbSet<CustomField> CustomFields { get; set; }

	public DbSet<CustomFieldValue> CustomFieldValues { get; set; }

	public DbSet<Transaction> Transactions { get; set; }

	public DbSet<Attachement> Attachements { get; set; }

	public DbSet<Setting> Settings { get; set; }

	public DbSet<TransactionHistory> TransactionHistories { get; set; }

	public DbSet<ExchangeRate> ExchangeRates { get; set; }

	public DbSet<AuditLog> AuditLogs { get; set; }

	public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
		: base(options)
	{
	}

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder.Properties<decimal>().HavePrecision(18, 3);
		base.ConfigureConventions(configurationBuilder);
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.Entity<Session>().HasOne((Session s) => s.User).WithMany((User u) => u.Sessions)
			.HasForeignKey((Session s) => s.UserId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<BusinessUser>().HasOne((BusinessUser bu) => bu.User).WithMany((User u) => u.BusinessUsers)
			.HasForeignKey((BusinessUser bu) => bu.UserId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<BusinessUser>().HasOne((BusinessUser bu) => bu.Business).WithMany((Business b) => b.BusinessUsers)
			.HasForeignKey((BusinessUser bu) => bu.BusinessId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<User>().Property((User u) => u.Username).HasMaxLength(100)
			.IsRequired();
		modelBuilder.Entity<User>().HasIndex((User u) => u.Username).IsUnique();
		modelBuilder.Entity<User>().Property((User u) => u.Email).HasMaxLength(256)
			.IsRequired();
		modelBuilder.Entity<User>().HasIndex((User u) => u.Email).IsUnique();
		modelBuilder.Entity<Book>().HasOne((Book b) => b.Business).WithMany((Business bus) => bus.Books)
			.HasForeignKey((Book b) => b.BusinessId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<Setting>().HasOne((Setting t) => t.Book).WithMany((Book b) => b.Settings)
			.HasForeignKey((Setting t) => t.BookId);
		modelBuilder.Entity<Contact>().HasOne((Contact c) => c.Business).WithMany((Business b) => b.Contacts)
			.HasForeignKey((Contact c) => c.BusinessId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<Category>().HasOne((Category cat) => cat.Business).WithMany((Business b) => b.Categories)
			.HasForeignKey((Category cat) => cat.BusinessId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<PaymentMethod>().HasOne((PaymentMethod cat) => cat.Business).WithMany((Business b) => b.PaymentMethods)
			.HasForeignKey((PaymentMethod cat) => cat.BusinessId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<CustomField>().HasOne((CustomField cf) => cf.Book).WithMany((Book b) => b.CustomFields)
			.HasForeignKey((CustomField cf) => cf.BookId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<CustomFieldValue>().HasOne((CustomFieldValue cfv) => cfv.Transaction).WithMany((Transaction t) => t.CustomFieldValues)
			.HasForeignKey((CustomFieldValue cfv) => cfv.TransactionId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<CustomFieldValue>().HasOne((CustomFieldValue cfv) => cfv.CustomField).WithMany((CustomField cf) => cf.CustomFieldValues)
			.HasForeignKey((CustomFieldValue cfv) => cfv.CustomFieldId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<Transaction>().HasOne((Transaction t) => t.Category).WithMany((Category c) => c.Transactions)
			.HasForeignKey((Transaction t) => t.CategoryId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(required: false);
		modelBuilder.Entity<Transaction>().HasOne((Transaction t) => t.PaymentMethod).WithMany((PaymentMethod p) => p.Transactions)
			.HasForeignKey((Transaction t) => t.PaymentMethodId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(required: false);
		modelBuilder.Entity<Transaction>().HasOne((Transaction t) => t.Book).WithMany((Book b) => b.Transactions)
			.HasForeignKey((Transaction t) => t.BookId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<Transaction>().HasOne((Transaction t) => t.User).WithMany((User b) => b.Transactions)
			.HasForeignKey((Transaction t) => t.UserId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<Transaction>().HasOne((Transaction t) => t.Contact).WithMany((Contact b) => b.Transactions)
			.HasForeignKey((Transaction t) => t.ContactId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(required: false);
		modelBuilder.Entity<Transaction>().Property((Transaction t) => t.Currency).HasConversion<string>()
			.HasMaxLength(3)
			.IsRequired();
		modelBuilder.Entity<Transaction>().HasIndex((Transaction t) => new { t.BookId, t.Currency });
		modelBuilder.Entity<Transaction>().HasIndex((Transaction t) => new { t.BookId, t.Date });
		modelBuilder.Entity<Transaction>().HasIndex((Transaction t) => new { t.BookId, t.Currency, t.Type });
		modelBuilder.Entity<Transaction>().Property((Transaction t) => t.ExchangeRate).HasPrecision(18, 6);
		modelBuilder.Entity<TransactionHistory>().Property((TransactionHistory t) => t.ExchangeRate).HasPrecision(18, 6);
		modelBuilder.Entity<TransactionHistory>().HasOne((TransactionHistory t) => t.Book).WithMany((Book b) => b.TransactionHistories)
			.HasForeignKey((TransactionHistory t) => t.BookId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<TransactionHistory>().HasOne((TransactionHistory t) => t.User).WithMany((User b) => b.TransactionHistories)
			.HasForeignKey((TransactionHistory t) => t.UserId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<TransactionHistory>().HasOne((TransactionHistory t) => t.Transaction).WithMany((Transaction b) => b.TransactionHistories)
			.HasForeignKey((TransactionHistory t) => t.TransactionId)
			.OnDelete(DeleteBehavior.SetNull)
			.IsRequired(required: false);
		modelBuilder.Entity<Attachement>().HasOne((Attachement a) => a.Transaction).WithMany((Transaction t) => t.Attachements)
			.HasForeignKey((Attachement a) => a.TransactionId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<ExchangeRate>().Property((ExchangeRate er) => er.Currency).HasConversion<string>()
			.HasMaxLength(3)
			.IsRequired();
		modelBuilder.Entity<ExchangeRate>().Property((ExchangeRate er) => er.Rate).HasPrecision(18, 6);
		modelBuilder.Entity<ExchangeRate>().HasOne((ExchangeRate er) => er.Book).WithMany()
			.HasForeignKey((ExchangeRate er) => er.BookId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<ExchangeRate>().HasOne((ExchangeRate er) => er.SetByUser).WithMany()
			.HasForeignKey((ExchangeRate er) => er.SetByUserId)
			.OnDelete(DeleteBehavior.Restrict);
		modelBuilder.Entity<ExchangeRate>().HasIndex((ExchangeRate er) => new { er.BookId, er.Currency, er.RateDate }).IsUnique();
		modelBuilder.Entity(delegate(EntityTypeBuilder<AuditLog> entity)
		{
			entity.ToTable("AuditLogs");
			entity.Property((AuditLog a) => a.DataJson).HasColumnType("nvarchar(max)");
			entity.HasIndex((AuditLog a) => a.OccurredAt).HasDatabaseName("IX_AuditLogs_OccurredAt");
			entity.HasIndex((AuditLog a) => new { a.UserId, a.OccurredAt }).HasDatabaseName("IX_AuditLogs_User_Time");
			entity.HasIndex((AuditLog a) => new { a.BusinessId, a.OccurredAt }).HasDatabaseName("IX_AuditLogs_Business_Time");
			entity.HasIndex((AuditLog a) => new { a.BookId, a.OccurredAt }).HasDatabaseName("IX_AuditLogs_Book_Time");
			entity.HasIndex((AuditLog a) => new { a.EntityName, a.EntityId }).HasDatabaseName("IX_AuditLogs_Entity");
			entity.HasIndex((AuditLog a) => a.CorrelationId).HasDatabaseName("IX_AuditLogs_Correlation");
			entity.HasIndex((AuditLog a) => new { a.Action, a.OccurredAt }).HasDatabaseName("IX_AuditLogs_Action_Time");
		});
	}
}
