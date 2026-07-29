# Contributing to Aqlan Dental Pro

Thank you for your interest in contributing! This guide covers the essential conventions for working with this codebase.

## Project Structure

```
aqlan-dental/
├── backend/
│   ├── AqlanDentalPro.sln              # Solution file
│   ├── src/
│   │   ├── AqlanDentalPro.Domain/       # Entities, enums, interfaces
│   │   ├── AqlanDentalPro.Application/  # DTOs, services, validators
│   │   ├── AqlanDentalPro.Infrastructure/ # Data access, external services
│   │   └── AqlanDentalPro.API/          # Controllers, middleware, Program.cs
│   └── tests/
│       └── AqlanDentalPro.UnitTests/    # xUnit tests
├── frontend/
│   └── (Next.js 14 app)                 # React + TypeScript + Tailwind CSS
└── .github/workflows/ci.yml             # CI pipeline
```

## Branching

- **`main`** is the only long-lived branch. All features branch off `main` and merge back via PR.
- Branch naming: `feat/<short-description>`, `fix/<short-description>`, `chore/<short-description>`
- Delete remote branches after merge.

## Backend (.NET 8)

### Adding Migrations

Use EF Core tools for all schema changes. Do **not** add raw SQL to `Program.cs`:

```bash
cd backend
dotnet ef migrations add <MigrationName> --project src/AqlanDentalPro.Infrastructure
```

### Architecture

- **Domain**: POCO entities, enums, value objects. No external dependencies.
- **Application**: Services, DTOs, CQRS handlers, FluentValidation validators.
- **Infrastructure**: EF Core `AppDbContext`, repositories, external integrations.
- **API**: Controllers, middleware, DI registration in `Program.cs`.

### Key Conventions

- **Passwords**: Argon2id hashing via `AuthService`. Never store plain text.
- **Soft Delete**: `BaseEntity` provides `DeletedAt`/`DeletedBy`. Use `IgnoreQueryFilters()` when needed.
- **Transactions**: Wrap multi-entity operations in explicit transactions.
- **Advisory Locks**: Use `pg_advisory_xact_lock` for concurrency-critical sections (e.g., queue operations).
- **Rate Limiting**: Policies defined in `Program.cs` (`AuthPolicy`, `BookingPolicy`, `PortalPolicy`).

### Writing Tests

- Place tests in `backend/tests/AqlanDentalPro.UnitTests/`.
- Use **xUnit** + **FluentAssertions** + **Moq**.
- Name test files to mirror the source: `AppointmentServiceTests.cs`, `FinanceValidatorTests.cs`.
- Test file structure mirrors source structure (`Validators/`, `Services/`, `Authorization/`).

```bash
cd backend
dotnet test --verbosity minimal
```

## Frontend (Next.js)

### Tech Stack

- **Next.js 14** with App Router
- **TypeScript** (strict mode)
- **Tailwind CSS** for styling
- **Vitest** + **React Testing Library** for tests

### Key Conventions

- Components in `app/` (page components) and `components/` (reusable).
- API calls via service functions in `lib/` or `services/`.
- Run `npx tsc --noEmit` and `npm run lint` before pushing.

### Writing Tests

```bash
cd frontend
npx vitest run --reporter=verbose
```

## CI Pipeline

- **Backend**: Restore → Build (`--warnaserror`) → Unit Tests
- **Frontend**: Install → Type check → Lint → Tests → Build
- CI runs on push to `main` and on all PRs targeting `main`.
- Failing tests will block merges.

## Pull Request Checklist

- [ ] Code compiles and all tests pass locally
- [ ] No `console.log` / `Console.WriteLine` debug statements
- [ ] New endpoints have proper authorization attributes
- [ ] New fields are added via EF Core migrations (not raw SQL)
- [ ] Frontend: TypeScript types are up to date
- [ ] Descriptive commit messages referencing relevant issues

## Security Notes

- Never commit secrets or API keys. Use environment variables.
- All staff endpoints require `[Authorize(Policy = "StaffOnly")]`.
- Admin-only actions require `[Authorize(Policy = "AdminOnly")]`.
- Patient portal endpoints use separate JWT scheme and policies.
