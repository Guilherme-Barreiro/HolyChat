using HolyChat.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HolyChat.Api.Data;

public class HolyChatDbContext : DbContext
{
    public HolyChatDbContext(DbContextOptions<HolyChatDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- User ----------
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.MatrixUserId).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();

            // FK recursiva: InvitedBy -> User
            // Restrict em vez de Cascade: apagar/alterar um User não deve
            // desencadear cascata automática sobre quem ele convidou
            // (soft-delete + revisão manual, como decidido).
            entity.HasOne(u => u.InvitedBy)
                  .WithMany(u => u.InvitedUsers)
                  .HasForeignKey(u => u.InvitedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Invite ----------
        modelBuilder.Entity<Invite>(entity =>
        {
            entity.HasIndex(i => i.Code).IsUnique();

            entity.HasOne(i => i.CreatedBy)
                  .WithMany(u => u.CreatedInvites)
                  .HasForeignKey(i => i.CreatedById)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.GrantedBy)
                  .WithMany()
                  .HasForeignKey(i => i.GrantedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- AuditLog ----------
        modelBuilder.Entity<AuditLog>(entity =>
        {
            // Action guardado como string na BD (legibilidade em debug),
            // ao contrário de Role/UserStatus/InviteStatus que ficam int.
            entity.Property(a => a.Action)
                  .HasConversion<string>()
                  .HasMaxLength(64);

            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.ActorId);
            entity.HasIndex(a => a.TargetId);

            entity.HasOne(a => a.Actor)
                  .WithMany()
                  .HasForeignKey(a => a.ActorId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Target)
                  .WithMany()
                  .HasForeignKey(a => a.TargetId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.RelatedInvite)
                  .WithMany()
                  .HasForeignKey(a => a.RelatedInviteId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
