using System.ComponentModel.DataAnnotations;

namespace UserMgmt.Api.Models
{
    public enum UserStatus
    {
        Unverified,
        Active,
        Blocked
    }

    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(320)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public UserStatus Status { get; set; } = UserStatus.Unverified;

        // Durable record of e-mail verification. Blocking must not erase it:
        // after a block/unblock cycle the account returns to Active only if
        // this flag is set, otherwise back to Unverified.
        public bool EmailVerified { get; set; }

        public Guid? VerificationToken { get; set; }

        public DateTime? LastLogin { get; set; }

        public DateTime? LastActivity { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
