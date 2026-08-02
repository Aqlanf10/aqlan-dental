# W11 — Journal and Accounting-Period Integrity

Status: implemented locally; PostgreSQL acceptance pending CI.

## Scope

- Enforce strict debit/credit XOR on every journal line.
- Enforce at least two lines and exact debit/credit balance at the database transaction boundary.
- Allow only one non-reversal journal entry per branch/source document, with the documented year-end-closing exception.
- Allow only one reversal per original entry.
- Allocate daily journal numbers atomically under concurrency.
- Reject create/post operations inside an active closed accounting period in the application service.
- Reject insert, update, delete, and line mutations in closed periods at the PostgreSQL layer.
- Permit only the official open-period reversal backlink mutation on an entry from a closed period.
- Keep reversal creation atomic while avoiding the circular self-referencing FK save dependency.

## Evidence

- Release builds for unit and integration projects succeed.
- Existing finance/journal regression set: 90/90 passed.
- W11 application period-lock tests: 3/3 passed.
- PostgreSQL integration tests cover strict line constraints, ten concurrent number reservations, duplicate source rejection, closed-period immutability, and official reversal.
- Local PostgreSQL execution is unavailable because Docker is not installed; the repository CI Testcontainers job is the required acceptance gate before merge.

Branch: `codex/w11-journal-period-locks`
