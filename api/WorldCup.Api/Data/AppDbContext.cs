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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(user => user.GoogleId)
            .IsUnique();

        modelBuilder.Entity<Prediction>()
            .HasIndex(prediction => new { prediction.BettingGroupId, prediction.UserId, prediction.MatchId })
            .IsUnique();

        modelBuilder.Entity<Prediction>()
            .HasOne(prediction => prediction.User)
            .WithMany()
            .HasForeignKey(prediction => prediction.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Prediction>()
            .HasOne(prediction => prediction.BettingGroup)
            .WithMany()
            .HasForeignKey(prediction => prediction.BettingGroupId)
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
            .HasOne(group => group.CreatedByUser)
            .WithMany()
            .HasForeignKey(group => group.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
