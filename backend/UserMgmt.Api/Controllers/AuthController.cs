using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UserMgmt.Api.Data;
using UserMgmt.Api.DTOs;
using UserMgmt.Api.Models;
using UserMgmt.Api.Services;

namespace UserMgmt.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TokenService _tokens;
        private readonly EmailDispatcher _emailQueue;

        public AuthController(AppDbContext db, TokenService tokens, EmailDispatcher emailQueue)
        {
            _db = db;
            _tokens = tokens;
            _emailQueue = emailQueue;
        }

        // Creates a new unverified account and sends a verification e-mail.
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Password))
                return BadRequest(new MessageResponse("Name, e-mail and password are required."));

            var email = req.Email.Trim().ToLowerInvariant();

            // An unverified account is reset and re-verified on its existing row,
            // so this path can never violate the unique index.
            var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existing != null && existing.Status == UserStatus.Unverified)
            {
                existing.Name = req.Name.Trim();
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
                existing.VerificationToken = Guid.NewGuid();

                await _db.SaveChangesAsync();

                await _emailQueue.EnqueueAsync(new EmailJob(existing.Email, existing.Name, existing.VerificationToken!.Value));

                return Ok(new MessageResponse("Registration successful. Please check your e-mail to verify your account."));
            }

            // No application-level duplicate check here: the unique index
            // idx_users_email_unique in the database is the single source of truth.
            // Inserting a duplicate e-mail makes PostgreSQL reject the write with
            // error 23505, which we catch below and turn into a friendly message.
            var user = new User
            {
                Name = req.Name.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                Status = UserStatus.Unverified,
                VerificationToken = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is PostgresException pg &&
                pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Conflict(new MessageResponse("An account with this e-mail already exists."));
            }

            // Queue the verification e-mail for reliable background delivery with retry.
            await _emailQueue.EnqueueAsync(new EmailJob(user.Email, user.Name, user.VerificationToken!.Value));

            return Ok(new MessageResponse("Registration successful. Please check your e-mail to verify your account."));
        }

        // Validates credentials and returns a JWT for active, verified users.
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.Trim().ToLowerInvariant());

            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new MessageResponse("Invalid e-mail or password."));

            if (user.Status == UserStatus.Blocked)
                return Unauthorized(new MessageResponse("This account has been blocked."));

            if (user.Status == UserStatus.Unverified)
                return Unauthorized(new MessageResponse("Please verify your e-mail before logging in."));

            user.LastLogin = DateTime.UtcNow;
            user.LastActivity = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var token = _tokens.CreateToken(user);
            return Ok(new AuthResponse(token, user.Id, user.Name, user.Email, user.Status.ToString().ToLowerInvariant()));
        }

        // Activates an account when the user clicks the link in the verification e-mail.
        [HttpGet("verify")]
        public async Task<IActionResult> Verify([FromQuery] Guid token)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);
            if (user == null)
                return NotFound(new MessageResponse("Invalid or expired verification link."));

            if (user.Status == UserStatus.Unverified)
            {
                user.Status = UserStatus.Active;
                await _db.SaveChangesAsync();
            }

            return Ok(new MessageResponse("E-mail verified. You can now log in."));
        }
    }
}
