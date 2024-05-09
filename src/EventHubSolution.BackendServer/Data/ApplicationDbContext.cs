using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Data.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EventHubSolution.BackendServer.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            IEnumerable<EntityEntry> modified = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added);
            foreach (EntityEntry item in modified)
            {
                if (item.Entity is IDateTracking changedOrAddedItem)
                {
                    if (item.State == EntityState.Added)
                    {
                        changedOrAddedItem.CreatedAt = DateTime.Now;
                    }
                    else
                    {
                        changedOrAddedItem.UpdatedAt = DateTime.Now;
                    }
                }
            }
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<IdentityRole>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<User>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Label>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Command>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Function>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Category>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Event>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Review>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<EmailLogger>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<FileStorage>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<EventSubImage>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<TicketType>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<EmailContent>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Payment>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Ticket>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Reason>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Conversation>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Message>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
        }

        public DbSet<User> Users { set; get; }
        public DbSet<Category> Categories { set; get; }
        public DbSet<Command> Commands { set; get; }
        public DbSet<CommandInFunction> CommandInFunctions { set; get; }
        public DbSet<EmailAttachment> EmailAttachments { set; get; }
        public DbSet<EmailContent> EmailContents { set; get; }
        public DbSet<EmailLogger> EmailLoggers { set; get; }
        public DbSet<Event> Events { set; get; }
        public DbSet<EventSubImage> EventSubImages { set; get; }
        public DbSet<FavouriteEvent> FavouriteEvents { set; get; }
        public DbSet<FileStorage> FileStorages { set; get; }
        public DbSet<Function> Functions { set; get; }
        public DbSet<Label> Labels { set; get; }
        public DbSet<LabelInEvent> LabelInEvents { set; get; }
        public DbSet<LabelInUser> LabelInUsers { set; get; }
        public DbSet<Payment> Payments { set; get; }
        public DbSet<Permission> Permissions { set; get; }
        public DbSet<Review> Reviews { set; get; }
        public DbSet<Ticket> Tickets { set; get; }
        public DbSet<TicketType> TicketTypes { set; get; }
        public DbSet<UserFollower> UserFollowers { set; get; }
        public DbSet<EventCategory> EventCategories { set; get; }
        public DbSet<Reason> Reasons { set; get; }
        public DbSet<Invitation> Invitations { set; get; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
    }
}
