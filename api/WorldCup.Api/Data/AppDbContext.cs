using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Models;

namespace WorldCup.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<BettingGroup> BettingGroups => Set<BettingGroup>();
    public DbSet<BettingGroupMember> BettingGroupMembers => Set<BettingGroupMember>();
    public DbSet<BettingGroupInviteLink> BettingGroupInviteLinks => Set<BettingGroupInviteLink>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatMessageReaction> ChatMessageReactions => Set<ChatMessageReaction>();
    public DbSet<PendingMatchFetch> PendingMatchFetches => Set<PendingMatchFetch>();
    public DbSet<ApiCallLog> ApiCallLogs => Set<ApiCallLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(user => user.GoogleId)
            .IsUnique();

        modelBuilder.Entity<Prediction>()
            .HasIndex(prediction => new { prediction.UserId, prediction.MatchId })
            .IsUnique();

        modelBuilder.Entity<Prediction>()
            .HasIndex(prediction => prediction.MatchId);

        modelBuilder.Entity<Prediction>()
            .HasOne(prediction => prediction.User)
            .WithMany()
            .HasForeignKey(prediction => prediction.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MatchResult>()
            .HasIndex(result => result.MatchId)
            .IsUnique();

        modelBuilder.Entity<Invitation>()
            .HasIndex(invitation => new { invitation.Email, invitation.BettingGroupId })
            .IsUnique();

        modelBuilder.Entity<Invitation>()
            .HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invitation>()
            .HasOne(invitation => invitation.BettingGroup)
            .WithMany()
            .HasForeignKey(invitation => invitation.BettingGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BettingGroupMember>()
            .HasIndex(member => new { member.BettingGroupId, member.UserId })
            .IsUnique();

        modelBuilder.Entity<BettingGroupMember>()
            .HasOne(member => member.BettingGroup)
            .WithMany(group => group.Members)
            .HasForeignKey(member => member.BettingGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BettingGroupMember>()
            .HasOne(member => member.User)
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BettingGroup>()
            .Property(group => group.EntryFee)
            .HasPrecision(18, 2);

        modelBuilder.Entity<BettingGroup>()
            .HasOne(group => group.CreatedByUser)
            .WithMany()
            .HasForeignKey(group => group.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BettingGroupInviteLink>()
            .HasIndex(link => link.Token)
            .IsUnique();

        modelBuilder.Entity<BettingGroupInviteLink>()
            .HasOne(link => link.BettingGroup)
            .WithMany()
            .HasForeignKey(link => link.BettingGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BettingGroupInviteLink>()
            .HasOne(link => link.CreatedByUser)
            .WithMany()
            .HasForeignKey(link => link.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(message => new { message.BettingGroupId, message.CreatedAt });

        modelBuilder.Entity<ChatMessage>()
            .Property(message => message.Content)
            .HasMaxLength(500)
            .IsRequired();

        modelBuilder.Entity<ChatMessage>()
            .Property(message => message.SenderDisplayNameOverride)
            .HasMaxLength(100);

        modelBuilder.Entity<MatchResult>()
            .Property(result => result.Referee)
            .HasMaxLength(100);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(message => message.BettingGroup)
            .WithMany()
            .HasForeignKey(message => message.BettingGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(message => message.User)
            .WithMany()
            .HasForeignKey(message => message.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessageReaction>()
            .HasIndex(r => new { r.ChatMessageId, r.UserId, r.Emoji })
            .IsUnique();

        modelBuilder.Entity<ChatMessageReaction>()
            .Property(r => r.Emoji)
            .HasMaxLength(10)
            .IsRequired();

        modelBuilder.Entity<ChatMessageReaction>()
            .HasOne(r => r.ChatMessage)
            .WithMany()
            .HasForeignKey(r => r.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessageReaction>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PendingMatchFetch>()
            .HasKey(pending => pending.MatchId);

        modelBuilder.Entity<PendingMatchFetch>()
            .Property(pending => pending.MatchId)
            .ValueGeneratedNever();

        modelBuilder.Entity<PendingMatchFetch>()
            .HasIndex(pending => pending.NextAttemptAt);

        modelBuilder.Entity<ApiCallLog>()
            .HasIndex(log => log.CalledAt);
    }
}
