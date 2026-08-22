# User Management App

Full-stack user management system: account registration with e-mail verification, JWT authentication, and an admin dashboard for managing users (search, sort, bulk block/unblock/delete).

## Tech stack

| Layer     | Technology                                        |
|-----------|---------------------------------------------------|
| Frontend  | React 18, Vite, Bootstrap 5, Axios                |
| Backend   | ASP.NET Core 8 Web API, Entity Framework Core 8   |
| Database  | PostgreSQL 16 (Npgsql provider)                   |
| E-mail    | SendGrid HTTP API (SMTP fallback for local dev)   |
| Hosting   | Render (static site + web service + managed Postgres) |

## Features

- **Registration** — creates an `Unverified` account and queues a verification e-mail (background dispatcher with retry).
- **E-mail confirmation** — one-time GUID link activates the account.
- **Login** — validates credentials with BCrypt and issues a signed JWT (12 h).
- **Users dashboard** — status badges (`Active` / `Blocked` / `Unverified`), relative last-seen times, debounced search, sortable columns.
- **Bulk actions** — block, unblock, delete selected users; delete all unverified users.
- **Session invalidation on self-block** — every request re-checks the caller's status (`ActiveUserFilter`); blocking your own account makes the next API call return `401 redirectToLogin`, and the frontend logs out automatically.
- **Re-registration over unverified accounts** — resends a fresh verification link instead of locking the address out.

## Storage-level consistency

E-mail uniqueness is **not** enforced by application-side pre-checks. A unique index in PostgreSQL is the single source of truth:

```csharp
// Data/AppDbContext.cs
u.HasIndex(x => x.Email).IsUnique().HasDatabaseName("idx_users_email_unique");
u.HasIndex(x => x.LastActivity).HasDatabaseName("idx_users_last_activity");
```

Registering a duplicate e-mail attempts the insert; PostgreSQL rejects it with error `23505`, which the API catches and converts into a friendly response:

```csharp
// Controllers/AuthController.cs
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
```

Resulting indexes in the database:

| Index                      | Purpose                                   |
|----------------------------|-------------------------------------------|
| `idx_users_email_unique`   | UNIQUE btree on `Email` — consistency guarantee |
| `idx_users_last_activity`  | speeds up the default "recently active" sort |
| `PK_Users`                 | primary key                               |

## Running locally

Prerequisites: .NET 8 SDK, Node.js 20+, Docker.

```bash
# 1. Start PostgreSQL
docker compose up db -d

# 2. Configure backend secrets
cd backend/UserMgmt.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "<64-char-random-hex>"
dotnet user-secrets set "Smtp:User" "<your-smtp-user>"       # or SendGrid creds:
dotnet user-secrets set "Smtp:Password" "<your-smtp-pass>"   # SendGrid:ApiKey / SendGrid:From

# 3. Run the API (http://localhost:8080)
dotnet run

# 4. Run the frontend (http://localhost:5173)
cd ../../frontend
npm install
npm run dev
```

Alternatively run everything containerized: `docker compose up --build`.

## Environment variables (production)

| Key                        | Example                              |
|----------------------------|--------------------------------------|
| `DATABASE_URL`             | `postgres://user:pass@host/dbname`   |
| `Jwt__Secret`              | 64-char random hex                   |
| `App__PublicUrl`           | `https://<frontend>.onrender.com`    |
| `App__CorsOrigins`         | `https://<frontend>.onrender.com`    |
| `SendGrid__ApiKey`         | `SG....`                             |
| `SendGrid__From`           | verified sender address              |

Note the double underscores — ASP.NET Core maps them to nested configuration keys (e.g. `App__PublicUrl` → `App:PublicUrl`).

## API overview

| Method | Route                        | Auth required | Description                          |
|--------|------------------------------|---------------|--------------------------------------|
| POST   | `/api/auth/register`         | no            | Create unverified account + send mail |
| GET    | `/api/auth/verify?token=`    | no            | Activate account via e-mail link      |
| POST   | `/api/auth/login`            | no            | Issue JWT                             |
| GET    | `/api/users?q=&sort=&dir=`   | yes           | List users (search + sorting)         |
| POST   | `/api/users/block`           | yes           | Block selected user ids               |
| POST   | `/api/users/unblock`         | yes           | Unblock selected user ids             |
| POST   | `/api/users/delete`          | yes           | Delete selected user ids              |
| POST   | `/api/users/delete-unverified` | yes        | Delete all never-verified accounts    |

## Project structure

```
backend/
  UserMgmt.Api/
    Controllers/   # AuthController, UsersController
    Services/      # TokenService, EmailService, EmailDispatcher (+hosted service), ActiveUserFilter
    Data/          # AppDbContext (EF Core model + indexes)
    Models/ DTOs/  # EF entity, request/response records
frontend/
  src/
    api/           # axios instance (JWT interceptor + auto-logout)
    pages/         # Login, Register, Verify, UsersPage
    components/    # Header
```
