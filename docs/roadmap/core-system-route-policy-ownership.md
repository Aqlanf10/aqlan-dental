# Core System Backend Policy Ownership

- Spec: `011-core-system-stabilization` (`CORE-REQ-005`, `CORE-REQ-007`)
- Slice: `CORE-P1-S4`
- Companion: `core-system-route-ownership.md` (`CORE-P1-S2`, frontend owner routes)
- Enforced by: `backend/tests/AqlanDentalPro.UnitTests/Authorization/RoutePolicyOwnershipTests.cs`

`CORE-P1-S2` settled which frontend route owns each capability. This slice settles the
other half: which backend policy guards it, and makes that answer executable rather than
descriptive.

## Why this is a test and not a table

`CORE-REQ-007` says server authorization is the authority and hidden UI controls do not
substitute for it. Nothing enforced that. A controller that lost its `[Authorize]` in a
refactor, had its policy swapped for a broader one, or gained a new `[AllowAnonymous]`
endpoint would compile, pass every existing test, and ship — the frontend would still hide
the button, so nothing would look wrong until someone called the endpoint directly.

The contract test reads the compiled assembly, not the source text, so it sees what ASP.NET
will actually enforce: attributes merged across partial classes and inherited from base
types. That distinction is not academic. `FinanceV3Controller` is spread over nine files,
only some of which carry an attribute, and `RadiographsController` is declared inside
`ClinicalPhotosController.cs` — a survey by filename misses it entirely, and did.

## What the contract asserts

| Check | Failure it prevents |
|---|---|
| Every controller enforces its declared policy | A policy silently swapped for a broader one |
| Every controller appears in the contract | A new controller whose authorization only its author has read |
| No stale entries | A rule that stopped protecting anything when its controller was renamed |
| No controller unprotected by accident | A controller with no class-level authorization at all |
| Every action has an explicit decision | An action added to an opt-in controller that forgot both attributes |
| The anonymous surface is exactly as declared | A new endpoint reachable with no authentication |
| Every named policy is registered | A typo'd policy name — ASP.NET raises it as a 500 on a live endpoint, not a build error |
| Queue-display middleware still guards its paths | Deleting the middleware while the `[AllowAnonymous]` attribute stays, publishing the live queue |

## Policy role sets

| Policy | Roles admitted |
|---|---|
| `AdminOnly`, `AdminAccess` | Admin |
| `StaffOnly` | any authenticated user that is **not** a Patient |
| `AdminOrReception` | Admin, Reception |
| `ReportsAccess`, `FinanceWrite`, `Commission*` | Admin, Accountant |
| `FinanceAccess`, `CashierAccess` | Admin, Reception, Accountant |
| `ClinicalRead`, `ClinicalWrite`, `DoctorAccess`, `AIAccess` | Admin, Orthodontist, GeneralDentist, OralSurgeon |
| `AppointmentAccess` | Admin, the three clinical roles, Reception |
| `OrthoAccess` | Admin, Orthodontist |
| `GeneralAccess` | Admin, GeneralDentist |
| `SurgeryAccess` | Admin, OralSurgeon |
| `OrthoSurgicalAccess` | Admin, Orthodontist, OralSurgeon |
| `PatientAccess` | Patient |

A class-level policy is a **floor**, not the whole story. Where an action carries its own
`[Authorize]`, the two combine and both must pass — so `RoomsSettingsController` defaults to
`StaffOnly` for the active-list read while every mutation stays `AdminOnly`.

## Deny-by-default hardening applied in this slice

Seven controllers carried no class-level policy and relied on each action opting in. **Every
action in all seven was correctly covered — no endpoint was exposed.** What was wrong was the
shape: an action added later that forgot both attributes would have been publicly reachable,
and on this list that is a serious place for it to happen.

| Controller | Default added | Why that policy |
|---|---|---|
| `AuthController` | `StaffOnly` | Issues tokens, unlocks accounts, impersonates users |
| `BookingRequestsController` | `AdminOrReception` | Mixes the public booking form with a staff queue holding patient names and phones |
| `PatientPortalController` | bare `[Authorize]` | Serves patients (`PatientAccess`) and staff (`AdminOrReception`); no single role policy fits both, so the default only requires authentication |
| `PublicController` | `StaffOnly` | Public website reads keep `[AllowAnonymous]`; `GetQueue` was already staff-gated |
| `CephNormsController` | `StaffOnly` | Reference data, not patient data; writes stay `AdminOnly` |
| `RoomsSettingsController` | `StaffOnly` | Active-list read is staff; writes stay `AdminOnly` |
| `ServicesSettingsController` | `StaffOnly` | Same shape |

`[AllowAnonymous]` overrides a class-level `[Authorize]`, so every genuinely public action
keeps working unchanged. Verified at runtime, not by reading.

## The unauthenticated surface

Twenty-one actions are reachable without a token, and the contract pins the list so it cannot
grow unnoticed: staff sign-in and recovery (5), patient-portal sign-in, recovery and the
pre-login clinic card (5), public-website booking (7), the waiting-room room list (1), the
queue display (1, see below) and file download (1).

Two deserve their reasons recorded:

- **`ClinicQueueController.GetDisplay`** carries `[AllowAnonymous]` but is **not** anonymous.
  `QueueDisplayAuthenticationMiddleware` (`CORE-PAT-020`) demands `StaffOnly` for its path
  before endpoint authorization runs, precisely so a loosened attribute cannot publish the
  live queue. Verified: the path answers 401 with no token. The contract pins the middleware's
  path list, because deleting the middleware would leave the attribute behind and silently
  expose patient names on a public URL with every test still green.
- **`UploadsController.Download`** is anonymous because `<img>` and `<a>` cannot carry a bearer
  token. It is guarded by a path-traversal check and an unguessable stored filename. That is
  weaker than authorization and is recorded here as a known limitation rather than presented
  as a control.

## Runtime verification

Against PostgreSQL with the real application, after the hardening:

- Anonymous and still 200: the four public-website reads, the portal clinic card, the
  waiting-room room list.
- Anonymous and correctly 401: `/api/auth/me`, `/api/patients`, `/api/settings/rooms`,
  `/api/settings/services`, `/api/ceph-norms`, `/api/public/queue`, `/api/booking-requests`,
  `/api/clinic-queue/display`, `/api/portal/dashboard`.
- Admin token and 200: all six staff endpoints above; login returns a token.
- Admin token against `/api/portal/dashboard`: **403** — a staff token is not a Patient token,
  so the portal boundary holds in both directions.

## What this slice does not claim

It pins current behaviour; it does not assert current behaviour is ideal. Where a policy looks
broader than a capability warrants — `StaffOnly` on lab and patient controllers admits every
non-patient role, and per-action permission keys do the finer work — the entry says so. The
value is that changing any of it now requires editing the contract, which puts it in front of
a reviewer instead of past one.
