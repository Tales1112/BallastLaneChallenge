# Integration verification

The suite uses the actual application services, repository implementations, SQLite provider, ASP.NET controllers and middleware. Application unit tests remain separate from these integration scenarios through the `Category=Integration` trait.

## Run

From the solution directory:

```powershell
# All backend tests
dotnet test -c Release -m:1

# SQLite and HTTP integration tests
dotnet test -c Release -m:1 --filter "Category=Integration"

# Only the separate Kestrel process with real HTTP connections
dotnet test -c Release -m:1 --filter "Transport=Kestrel"

# Save a machine-readable report
dotnet test -c Release -m:1 --logger "trx;LogFileName=results.trx"
```

The .NET SDK must be on PATH. The Kestrel test requires permission to create a child process and open a local loopback port. No external database, Docker or application instance is required. Compiled frontend assets are included in the project; rebuild them with `npm run build` in `frontend` after changing frontend source.

## Coverage

| Area | Scenarios |
| --- | --- |
| Authentication | Registration, login, incorrect credentials, logout, protected and public endpoints, invalid input, normalized/duplicate email, simultaneous duplicate registration |
| CRUD | Create/read/update/delete, Location header, missing records, all statuses, optional fields, calendar dates, length boundaries, audit identity/time preservation |
| Ownership | Separate user lists, cross-user read/update/delete rejection, ignoring an injected owner in a request body |
| Rejected changes | Invalid updates preserve the original task; failed validation and CSRF checks do not write or delete data |
| CSRF | Missing/adulterated tokens, another browser's token, stale tokens after login/logout, valid refreshed-token recovery |
| Persistence | Real SQLite roundtrips, parameterized SQL text, foreign keys, unique email, account/task preservation after host restart, persistent deletion |
| Initialization | An empty configured database, distinct databases per host, demo credentials and six tasks, repeatable seeding without overwriting edits |
| Web configuration | Session-cookie attributes, API cache policy, security headers, production Secure cookies and HTTPS redirection |
| Frontend hosting | MVC host HTML and compiled JavaScript/CSS are served successfully |
| Real transport | A separate Kestrel process handles seeded login, registration, private data, full CRUD and logout through real HTTP sockets |

`ApiTests` and `RepositoryTests` retain the original integration scenarios. Additional scenarios live in `tests/TaskFlow.Tests/Integration`. Shared factories and HTTP helpers are in its `Support` folder.

## Isolation and cleanup

Each test application gets a unique SQLite file in the operating system's temporary directory. Restart scenarios explicitly reuse only their own temporary file between hosts. Test cleanup closes hosts and removes the temporary database and SQLite sidecar files. The Kestrel process binds to `127.0.0.1:0`, so the OS assigns a free port, and the test terminates its own process in `finally`.

The production configuration test supplies HTTPS port 443 to the in-memory test host to assert redirection. It checks middleware behavior and Secure-cookie issuance; it does not provision or validate a TLS certificate.

The suite verifies API/application/database integration and static frontend delivery. It does not automate browser interactions, measure line coverage or perform load testing.

## Verified result — 2026-10-18

- Backend Release suite: **65 passed, 0 failed, 0 skipped**.
- Of these, **50 are integration tests** and **15 are application unit tests**.
- The integration set includes the separate Kestrel process with real HTTP.
- Frontend: **6 passed, 0 failed**; production build succeeded.
- No application-code changes were needed to make these scenarios pass.

The backend results were saved as a TRX report alongside the source ZIP.
