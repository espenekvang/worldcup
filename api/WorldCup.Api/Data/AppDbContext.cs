using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Models;

namespace WorldCup.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();

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
            .HasOne(prediction => prediction.User)
            .WithMany()
            .HasForeignKey(prediction => prediction.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MatchResult>()
            .HasIndex(result => result.MatchId)
            .IsUnique();

        modelBuilder.Entity<Invitation>()
            .HasIndex(invitation => invitation.Email)
            .IsUnique();

        modelBuilder.Entity<Invitation>()
            .HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
