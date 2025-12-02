using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebMessenger.Entities;

namespace WebMessenger
{
    public class AppDbContext : IdentityUserContext<User, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Chat> Chats => Set<Chat>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<Participant> Participants => Set<Participant>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
            modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
            modelBuilder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");

            modelBuilder.Entity<User>()
                .HasMany(u => u.Chats)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Chat>()
                .HasMany(c => c.Participants)
                .WithOne(p => p.Chat)
                .HasForeignKey(p => p.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Chat>()
                .HasMany(c => c.Messages)
                .WithOne(m => m.Chat)
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Participant>()
                .HasIndex(p => new { p.ChatId, p.UserId })
                .IsUnique();

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasMany(m => m.Attachments)
                .WithOne(a => a.Message)
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Attachment>()
                .HasDiscriminator<AttachmentType>("Type")
                .HasValue<FileAttachment>(AttachmentType.File)
                .HasValue<ImageAttachment>(AttachmentType.Image)
                .HasValue<AudioAttachment>(AttachmentType.Audio)
                .HasValue<VideoAttachment>(AttachmentType.Video)
                .HasValue<DocumentAttachment>(AttachmentType.Document)
                .HasValue<LinkAttachment>(AttachmentType.Link)
                .HasValue<LocationAttachment>(AttachmentType.Location);

            modelBuilder.Entity<Message>()
                .Property(m => m.Text)
                .HasMaxLength(5000);

            modelBuilder.Entity<FileAttachment>()
                .Property(f => f.Url)
                .HasMaxLength(2048);

            modelBuilder.Entity<FileAttachment>()
                .Property(f => f.Name)
                .HasMaxLength(255);

            modelBuilder.Entity<LinkAttachment>()
                .Property(l => l.Url)
                .HasMaxLength(2048);

            modelBuilder.Entity<Chat>()
                .Property(c => c.Name)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<User>()
                .Property(u => u.DisplayName)
                .HasMaxLength(50);
        }
    }
}