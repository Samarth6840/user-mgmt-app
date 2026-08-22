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

            query = (sort, dir) switch
            {
                ("email", "asc") => query.OrderBy(u => u.Email),
                ("email", "desc") => query.OrderByDescending(u => u.Email),
                ("name", "asc") => query.OrderBy(u => u.Name),
                ("name", "desc") => query.OrderByDescending(u => u.Name),
                (_, "asc") => query.OrderBy(u => u.LastActivity ?? u.CreatedAt),
                _ => query.OrderByDescending(u => u.LastActivity ?? u.CreatedAt),
            };

            var users = await query.Select(u => new UserRow(
                u.Id, u.Name, u.Email, u.Status.ToString().ToLower(), u.LastLogin, u.LastActivity, u.CreatedAt
            )).ToListAsync();

            return Ok(users);
        }

        // Marks one or more users as blocked in a single database round-trip.
        [HttpPost("block")]
        public async Task<IActionResult> Block([FromBody] BulkIdsRequest req)
        {
            if (req.Ids.Count == 0) return BadRequest(new MessageResponse("No users selected."));

            var affected = await _db.Users.Where(u => req.Ids.Contains(u.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Blocked));

            return Ok(new MessageResponse($"{affected} user(s) blocked."));
        }

        // Re-activates previously blocked users.
        [HttpPost("unblock")]
        public async Task<IActionResult> Unblock([FromBody] BulkIdsRequest req)
        {
            if (req.Ids.Count == 0) return BadRequest(new MessageResponse("No users selected."));

            var affected = await _db.Users.Where(u => req.Ids.Contains(u.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Active));

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
