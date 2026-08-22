namespace UserMgmt.Api.DTOs
{
    public record RegisterRequest(string Name, string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record AuthResponse(string Token, Guid Id, string Name, string Email, string Status);
    public record UserRow(Guid Id, string Name, string Email, string Status, DateTime? LastLogin, DateTime? LastActivity, DateTime CreatedAt);
    public record BulkIdsRequest(List<Guid> Ids);
    public record MessageResponse(string Message);
}
