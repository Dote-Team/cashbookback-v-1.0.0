using cashbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace cashbook.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Session has FK to User
            modelBuilder.Entity<Session>()
                .HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // BusinessUser has FK to User and Business
            modelBuilder.Entity<BusinessUser>()
                .HasOne(bu => bu.User)
                .WithMany(u => u.BusinessUsers)
                .HasForeignKey(bu => bu.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BusinessUser>()
                .HasOne(bu => bu.Business)
                .WithMany(b => b.BusinessUsers)
                .HasForeignKey(bu => bu.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);




            // Book has FK to Business
            modelBuilder.Entity<Book>()
                .HasOne(b => b.Business)
                .WithMany(bus => bus.Books)
                .HasForeignKey(b => b.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Setting>()
                .HasOne(t => t.Book)
                .WithMany(b => b.Settings)
                .HasForeignKey(t => t.BookId);
            //.OnDelete(DeleteBehavior.Restrict);

            // Contact has FK to Business
            modelBuilder.Entity<Contact>()
                .HasOne(c => c.Business)
                .WithMany(b => b.Contacts)
                .HasForeignKey(c => c.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);

            // Category has FK to Business
            modelBuilder.Entity<Category>()
                .HasOne(cat => cat.Business)
                .WithMany(b => b.Categories)
                .HasForeignKey(cat => cat.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PaymentMethod>()
               .HasOne(cat => cat.Business)
               .WithMany(b => b.PaymentMethods)
               .HasForeignKey(cat => cat.BusinessId)
               .OnDelete(DeleteBehavior.Restrict);

            // CustomField has FK to Book and Transaction
            modelBuilder.Entity<CustomField>()
                .HasOne(cf => cf.Book)
                .WithMany(b => b.CustomFields)
                .HasForeignKey(cf => cf.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            // CustomFieldValues has FK to transaction and CustomField

            modelBuilder.Entity<CustomFieldValue>()
                .HasOne(cfv => cfv.Transaction)
                .WithMany(t => t.CustomFieldValues)
                .HasForeignKey(cfv => cfv.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomFieldValue>()
                .HasOne(cfv => cfv.CustomField)
                .WithOne(cf => cf.CustomFieldValues)
                .HasForeignKey<CustomFieldValue>(cfv => cfv.CustomFieldId)
                .OnDelete(DeleteBehavior.Restrict);



            // Transaction has FK to contact, Category, and Book ,user
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.PaymentMethod)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);


            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Book)
                .WithMany(b => b.Transactions)
                .HasForeignKey(t => t.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.User)
                .WithMany(b => b.Transactions)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Contact)
                .WithMany(b => b.Transactions)
                .HasForeignKey(t => t.ContactId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            //TransactionHistory has fk with book , user , transaction
            modelBuilder.Entity<TransactionHistory>()
               .HasOne(t => t.Book)
               .WithMany(b => b.TransactionHistories)
               .HasForeignKey(t => t.BookId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TransactionHistory>()
                .HasOne(t => t.User)
                .WithMany(b => b.TransactionHistories)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TransactionHistory>()
                .HasOne(t => t.Transaction)
                .WithMany(b => b.TransactionHistories)
                .HasForeignKey(t => t.TransactionId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);



            // Attachment has FK to Transaction
            modelBuilder.Entity<Attachement>()
                .HasOne(a => a.Transaction)
                .WithMany(t => t.Attachements)
                .HasForeignKey(a => a.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
