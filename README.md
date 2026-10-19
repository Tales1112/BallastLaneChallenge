
## User story

> As a person managing my own work, I want a private account where I can capture tasks, set dates, track progress and remove tasks I no longer need, so I can focus on the next meaningful step.

Acceptance criteria:

- I can register, sign in, see my account and sign out.
- I can create, view, edit and delete my own tasks; changes survive a restart.
- Another account cannot list, read, update or delete my tasks.
- Tasks contain a unique ID, title, description, status and optional due date.
- Invalid input produces useful feedback and does not write a record.
- I can search, filter and sort my tasks on a responsive interface.

## Architecture

```text
React UI → MVC / Web API → Application → Domain
                               ↑
                         Infrastructure → SQLite
```

| Project | Responsibility |
| --- | --- |
| `TaskFlow.Domain` | Immutable user/task models and task status |
| `TaskFlow.Application` | Use cases, validation and repository/password interfaces |
| `TaskFlow.Infrastructure` | SQLite implementation, parameterized SQL, password hashing |
| `TaskFlow.Web` | MVC view, API controllers, cookies, CSRF, rate limiting, composition root |
| `TaskFlow.Tests` | Application unit tests and real SQLite/HTTP integration tests |
| `frontend` | React components, API client, local view state, styling and frontend tests |

Domain has no project/package dependencies. Application depends only on Domain. Infrastructure implements Application interfaces. Web wires implementations through built-in dependency injection. Business services have no dependency on HTTP, SQLite or the frontend.

**Two API areas:** `/api/tasks` is the task API; `/api/auth` is the identity API. They are hosted in one process, with separate controllers and responsibilities. The exercise's “second API” is interpreted as a separate API area, not a separately deployed service. This keeps the demonstration simple; independent services would require an explicit token/identity trust boundary.

MVC is used for the host page (`HomeController` + Razor view); interactive frontend behavior is implemented in React. The same origin serves both UI and API, avoiding CORS and browser token storage.

## API contract

All mutations require an antiforgery token, including registration and login. Request `GET /api/auth/csrf`, retain its cookie and send the JSON response's `token` in `X-CSRF-TOKEN`. Refresh the token after login, registration or logout because the current identity changes. Also retain the `TaskFlow.Session` cookie. The React client handles this automatically.

| Method | Route | Access | Success |
| --- | --- | --- | --- |
| GET | `/api/auth/public` | Anonymous | 200 |
| GET | `/api/auth/csrf` | Anonymous | 200 |
| POST | `/api/auth/register` | Anonymous + CSRF | 201 + signed-in cookie |
| POST | `/api/auth/login` | Anonymous + CSRF | 200 + signed-in cookie |
| GET | `/api/auth/me` | Authenticated | 200 |
| POST | `/api/auth/logout` | Authenticated + CSRF | 204 |
| GET | `/api/tasks` | Authenticated | 200 |
| GET | `/api/tasks/{id}` | Owner | 200 / 404 |
| POST | `/api/tasks` | Authenticated + CSRF | 201 + Location header |
| PUT | `/api/tasks/{id}` | Owner + CSRF | 200 / 404 |
| DELETE | `/api/tasks/{id}` | Owner + CSRF | 204 / 404 |

Task request:

```json
{
  "title": "Prepare the release",
  "description": "Review the acceptance criteria",
  "status": "inProgress",
  "dueDate": "2026-11-01"
}
```

Register: `{ "name": "Alex Morgan", "email": "alex@example.com", "password": "Password123!" }`.
Login: `{ "email": "alex@example.com", "password": "Password123!" }`.

Rules: trimmed title 1–120 chars, description at most 2,000 chars, status `todo`, `inProgress` or `done`, optional date in `yyyy-MM-dd`. Past dates are allowed so overdue work can be represented. Dates are calendar dates, while audit timestamps use UTC. Name is 2–80 chars; email is normalized to lowercase and unique; password is 10–128 chars and must contain a letter and a number. Missing and invalid input returns 400, duplicate email 409, unauthenticated access 401, missing/not-owned tasks 404, excessive auth attempts 429. Validation errors use Problem Details. Unhandled errors return a generic 500 response.

## Data and security choices

- Direct `Microsoft.Data.Sqlite.Core` + SQLitePCLRaw native provider, with asynchronous repository interfaces and a connection per operation. SQLite's underlying I/O is synchronous; this is appropriate for a small local demo, not a scalability claim.
- `Users` and `Tasks` tables, foreign key, unique email, database checks and an index on `(UserId, CreatedAt)`. Startup creates an absent schema idempotently. A versioned migration system would be needed as the schema evolves.
- Every task query includes the authenticated user's ID. The API never accepts the owner from a request body.
- Passwords use ASP.NET's salted PBKDF2 password hasher. Hashes never appear in API responses. Unknown users still run password verification.
- HttpOnly, SameSite=Strict session cookie, CSRF tokens and a same-origin frontend. Cookies expire after eight hours with sliding renewal. Production requires HTTPS, enables HSTS and Secure cookies.
- Login/registration share a per-IP limit of 10 attempts/minute. For a multi-instance deployment, use a shared limiter and trusted proxy configuration.
- Content Security Policy, no third-party scripts/fonts, no HTML injection and no browser local-storage tokens.
- Default database: `src/TaskFlow.Web/App_Data/taskflow.db`. Override with `Database__Path`. Do not commit databases, secrets or demo sessions.
- For production, configure allowed hosts, persistent encrypted data-protection keys, backups, deployment secrets, verified email and a password recovery flow. These deployment features are outside this local interview implementation.

## Tests

```powershell
dotnet test -m:1
cd frontend
npm test
npm run build
```

Application unit tests cover validation before writes, normalization, hashes, login failures, duplicate accounts and ownership. Repository integration tests use disposable SQLite files and cover persistence, CRUD, unique email, foreign keys and SQL-like text. HTTP integration tests use `WebApplicationFactory` with an isolated database per test and cover cookies, CSRF, anonymous/protected endpoints, CRUD status codes, invalid JSON, validation, rate limits, cross-user access and MVC assets. Frontend tests cover view/search/status composition, sorting and local date behavior.

Run only the integration tests, or only the real HTTP smoke test:

```powershell
dotnet test -c Release -m:1 --filter "Category=Integration"
dotnet test -c Release -m:1 --filter "Transport=Kestrel"
```

Integration tests also cover full host restarts, repeatable demo seeding, concurrent duplicate registration, rejected mutations preserving data, CSRF identity/session binding and production cookie/redirect configuration. The Kestrel test starts a separate process on an automatically assigned loopback port, uses its own temporary database, and stops that process after the test. It does not use the running application on port 5180.

The test sources were started before the use-case implementation. The first attempted test run was blocked by package restoration; later integration failures were reproduced and corrected. The project does **not** claim a fully recorded red/green TDD history or a measured line-coverage percentage. The commit history should not be fabricated.

The requested Generative AI tools section and its evaluation criterion are excluded.

## Development and publishing

`npm run dev` runs Vite with `/api` proxied to the .NET server. For the final demonstration, use the compiled same-origin version on port 5180. Rebuild the frontend before publishing:

```powershell
cd frontend
npm ci
npm run build
cd ..
dotnet publish src/TaskFlow.Web -c Release -o publish
```

Configure the deployed host and HTTPS before running a production build. No cloud deployment, GitHub push or external account setup is included.
