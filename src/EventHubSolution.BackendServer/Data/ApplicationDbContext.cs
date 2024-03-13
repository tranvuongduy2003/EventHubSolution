using EventHubSolution.BackendServer.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventHubSolution.BackendServer.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
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
            builder.Entity<Location>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<EventSubImage>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<TicketType>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<EmailContent>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Payment>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);
            builder.Entity<Ticket>().Property(x => x.Id).HasMaxLength(50).IsUnicode(false);

            builder.Entity<Permission>()
                       .HasKey(x => new { x.RoleId, x.FunctionId, x.CommandId });

            builder.Entity<CommandInFunction>()
                       .HasKey(x => new { x.CommandId, x.FunctionId });

            builder.Entity<EmailAttachment>()
                       .HasKey(x => new { x.AttachmentId, x.EmailContentId });

            builder.Entity<LabelInEvent>()
                      .HasKey(x => new { x.LabelId, x.EventId });

            builder.Entity<UserFollower>()
                      .HasKey(x => new { x.FollowerId, x.FollowedId });

            builder.Entity<FavouriteEvent>()
                      .HasKey(x => new { x.EventId, x.UserId });

            builder.Entity<LabelInUser>()
                      .HasKey(x => new { x.LabelId, x.UserId });

            builder.Entity<EmailAttachment>()
                      .HasKey(x => new { x.AttachmentId, x.EmailContentId });
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
        public DbSet<Location> Locations { set; get; }
        public DbSet<Payment> Payments { set; get; }
        public DbSet<Permission> Permissions { set; get; }
        public DbSet<Review> Reviews { set; get; }
        public DbSet<Ticket> Tickets { set; get; }
        public DbSet<TicketType> TicketTypes { set; get; }
        public DbSet<UserFollower> UserFollowers { set; get; }
    }
}
