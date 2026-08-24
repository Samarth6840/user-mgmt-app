using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserMgmt.Api.Data;
using UserMgmt.Api.DTOs;
using UserMgmt.Api.Models;
using UserMgmt.Api.Services;

namespace UserMgmt.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    [ServiceFilter(typeof(ActiveUserFilter))]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UsersController(AppDbContext db) => _db = db;

        // Returns the user list with optional search, sort, and direction parameters.
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string sort = "last_activity", [FromQuery] string dir = "desc")
        {
            var query = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = q.Trim().ToLowerInvariant();
                query = query.Where(u => u.Name.ToLower().Contains(like) || u.Email.ToLower().Contains(like));
            }

            // Id tiebreaker keeps the ordering deterministic when the primary
            // key values are equal (otherwise rows can appear to shuffle).
            query = (sort, dir) switch
            {
                ("email", "asc") => query.OrderBy(u => u.Email).ThenBy(u => u.Id),
                ("email", "desc") => query.OrderByDescending(u => u.Email).ThenByDescending(u => u.Id),
                ("name", "asc") => query.OrderBy(u => u.Name).ThenBy(u => u.Id),
                ("name", "desc") => query.OrderByDescending(u => u.Name).ThenByDescending(u => u.Id),
                (_, "asc") => query.OrderBy(u => u.LastActivity ?? u.CreatedAt).ThenBy(u => u.Id),
                _ => query.OrderByDescending(u => u.LastActivity ?? u.CreatedAt).ThenByDescending(u => u.Id),
            };

            var users = await query.Select(u => new UserRow(
                u.Id, u.Name, u.Email, u.Status.ToString().ToLower(), u.LastLogin, u.LastActivity, u.CreatedAt
            )).ToListAsync();

            return Ok(users);
        }

        // Marks one or more users as blocked in a single database round-trip.
        // Only Status changes: LastLogin/LastActivity are login/session facts,
        // not record-edit timestamps, and must stay untouched.
        [HttpPost("block")]
        public async Task<IActionResult> Block([FromBody] BulkIdsRequest req)
        {
            if (req.Ids.Count == 0) return BadRequest(new MessageResponse("No users selected."));

            var affected = await _db.Users.Where(u => req.Ids.Contains(u.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Blocked));

            return Ok(new MessageResponse($"{affected} user(s) blocked."));
        }

        // Re-activates previously blocked users. The status before the block is
        // restored: a verified e-mail returns the account to Active, while an
        // account that never verified goes back to Unverified.
        [HttpPost("unblock")]
        public async Task<IActionResult> Unblock([FromBody] BulkIdsRequest req)
        {
            if (req.Ids.Count == 0) return BadRequest(new MessageResponse("No users selected."));

            var affected = await _db.Users.Where(u => req.Ids.Contains(u.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(
                    u => u.Status,
                    u => u.EmailVerified ? UserStatus.Active : UserStatus.Unverified));

            return Ok(new MessageResponse($"{affected} user(s) unblocked."));
        }

        // Permanently removes selected users from the database.
        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] BulkIdsRequest req)
        {
            if (req.Ids.Count == 0) return BadRequest(new MessageResponse("No users selected."));

            var affected = await _db.Users.Where(u => req.Ids.Contains(u.Id)).ExecuteDeleteAsync();

            return Ok(new MessageResponse($"{affected} user(s) deleted."));
        }

        // Bulk-deletes every user who never verified their e-mail address.
        [HttpPost("delete-unverified")]
        public async Task<IActionResult> DeleteUnverified()
        {
            var affected = await _db.Users.Where(u => u.Status == UserStatus.Unverified).ExecuteDeleteAsync();
            return Ok(new MessageResponse($"{affected} unverified user(s) deleted."));
        }
    }
}
