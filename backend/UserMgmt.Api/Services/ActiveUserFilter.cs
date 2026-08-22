using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using UserMgmt.Api.Data;

namespace UserMgmt.Api.Services
{
    // Runs before every [Authorize] request on the users controller.
    // It verifies the caller still exists and isn't blocked, then records their last-seen time.
    public class ActiveUserFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _db;
        public ActiveUserFilter(AppDbContext db) => _db = db;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var idClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? context.HttpContext.User.FindFirstValue("sub");

            if (idClaim == null || !Guid.TryParse(idClaim, out var userId))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Not authenticated." });
                return;
            }

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Account no longer exists. Please log in again.", redirectToLogin = true });
                return;
            }

            if (user.Status == Models.UserStatus.Blocked)
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Your account has been blocked. Please log in again.", redirectToLogin = true });
                return;
            }

            // Touch the last-seen timestamp so the admin dashboard stays up to date.
            await _db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastActivity, DateTime.UtcNow));

            await next();
        }
    }
}
