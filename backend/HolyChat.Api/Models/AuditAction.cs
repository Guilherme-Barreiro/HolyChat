namespace HolyChat.Api.Models;

public enum AuditAction
{
    UserRegistered,
    UserApproved,
    UserRejected,
    UserSuspended,
    UserReactivated,
    UserRemoved,
    UserPromoted,      // Member -> Admin
    UserDemoted,       // Admin -> Member
    InviteCreated,
    InviteRevoked,
    InviteUsed,
    InviteExpired,
    InviteBulkGranted, // admin atribuiu convite multi-uso
    AdminLogin
}
