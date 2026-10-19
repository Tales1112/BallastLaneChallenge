# Interview demonstration

## Opening: the problem (1 minute)

“TaskFlow helps a person manage their own tasks in a private workspace. The user can capture an idea, set a due date, move work from to-do to in-progress to completed, and remove obsolete work.”

Show the user story in the README and explain its acceptance criteria. The goal is to demonstrate the complete flow from a React action to an API use case and durable storage.

## Live application (3–4 minutes)

1. Start the local app and sign in using the seeded demo account.
2. Explain the summary, task cards, status filters, search and due-date sorting.
3. Create a task with a title, description and due date. Show it immediately in the list.
4. Edit its description and status. Mark it completed, then reopen it.
5. Reload the page to demonstrate persistence.
6. Delete the task and show the confirmation step.
7. Sign out. Create a second account and show its empty workspace.
8. Explain that task ownership is enforced in SQL, not just by hiding cards.

If demonstrating auth/API directly, show the anonymous `/api/auth/public` response and that `/api/auth/me` returns 401 when signed out. Mutating API requests require a current CSRF token and matching cookies; use the browser app for the main demo.

## Architecture walkthrough (3 minutes)

Walk through `TasksController.Create` → `TaskService.Create` → `ITaskRepository` → `SqliteTaskRepository.Add`.

- Controller: HTTP, authenticated identity and status code.
- Application: validation and use-case orchestration.
- Domain: data types; no framework coupling.
- Infrastructure: parameterized SQL and SQLite persistence.
- Composition root: binds interfaces without reversing the dependency direction.

Show an ownership query containing both task ID and user ID. Discuss returning 404 for a missing or another user's task to avoid disclosing its existence.

Show the separate identity API controller. Explain the one-process deployment interpretation and why cookies plus CSRF are a good fit for a same-origin app. A public/mobile API would warrant a different authentication strategy.

## Testing and tradeoffs (2 minutes)

Run `dotnet test -m:1` and `npm test` from the frontend directory. Open one unit test and one HTTP ownership test. Distinguish unit tests from repository/HTTP integration tests; do not describe all of them as isolated unit tests.

Mention that validation tests were authored early and integration tests exposed and verified a database configuration fix. Avoid claiming a full recorded TDD process or a coverage percentage.

Tradeoffs to discuss:

- SQLite needs no service setup and is suitable for the interview; high-volume concurrent writes would favor a server database.
- SQLite provider calls expose asynchronous APIs, but SQLite performs synchronous I/O internally.
- Client-side filtering keeps a small personal workspace simple; large datasets need paginated server queries.
- Updates currently use last-write-wins. A multi-user collaborative product would need optimistic concurrency/versioning.
- Idempotent table creation is enough for the first schema; production evolution needs versioned migrations.
- Stateless signed cookies are simple, but forced session revocation and password changes would require a session/security-stamp strategy.
- Registration conflicts disclose that an email exists; the demo favors clear feedback. Public-facing identity requirements may favor a generic response.

## Manual checklist

- Login, logout and registration work; new account has no demo tasks.
- Empty title, invalid email and short passwords show feedback.
- A valid task survives reload and server restart.
- Edit status/date, clear optional date, complete/reopen and delete.
- Search matches title and description; combined status/view filters work.
- Dates are displayed on the same calendar day entered; past dates are accepted and marked overdue.
- Responsive checks at desktop, tablet and 390px mobile; no horizontal page overflow.
- Keyboard navigation, labels, modal focus, Escape and visible focus states.
- Network errors preserve unsaved modal input; failed saves do not show success notices.
- Inspect browser console and network errors before screen sharing.

No Generative AI tools presentation is required for this scope.
