# Core System Route Ownership

- Spec: `011-core-system-stabilization`
- Slice: `CORE-P1-S2`
- Scope: dashboard capabilities with legacy, duplicate, or competing entry routes
- Runtime authority: backend authorization remains authoritative

## Ownership Rules

1. A capability has one active frontend owner route.
2. A legacy route contains no feature UI and performs only a server redirect.
3. Static aliases are registered in `frontend/src/lib/canonicalRoutes.ts`; an
   unregistered alias fails closed.
4. Dynamic aliases preserve supported query parameters, encode path identifiers,
   and cannot override owner-controlled parameters.
5. Aliases remain explicit in the route guard until they are removed, with access
   no broader than the canonical owner.
6. Backend policy ownership is recorded and tested separately in `CORE-P1-S4`.

## Core Journey Owners

| Capability | Canonical owner | Other entry routes | Ownership status |
|---|---|---|---|
| Patient list and record | `/patients`, `/patients/[id]` | `/patient-journey/[patientId]` | Alias redirects to the patient record with `focus=journey` |
| Appointment booking and recall | `/appointments` | None | Single active owner |
| Check-in and today's journey | `/daily-operations` | `/patient-journey` | Alias redirects to the owner |
| Queue | `/daily-operations?tab=queue` | `/clinic-queue` | Alias redirects to the queue tab |
| Doctor clinic | `/doctor-clinic` | Specialty workspaces are separate clinical capabilities | Single active owner for visit execution |
| Lab orders | `/lab` | `/lab/dashboard`, `/lab/payables`, `/lab/reports` are management views | Single order owner; management views remain distinct |
| Accounting | `/finance-v3` | Patient financial tabs are patient context, not a second ledger | Single ledger owner |
| Next appointment | `/appointments` | Appointment actions embedded in context link back to the owner | Single active owner |

## Supporting Capability Owners

| Capability | Canonical owner | Legacy aliases | Status |
|---|---|---|---|
| Users, roles, and permissions | `/settings?tab=permissions` | `/users`, `/settings/users`, `/settings/permissions` | Checked static redirects |
| General settings | `/settings` | None | Single active owner |
| Human resources | `/hr/attendance` plus explicit HR subroutes | `/hr` | Bare index redirects to attendance |
| Orthodontics and joint surgical planning | `/ortho`, `/ortho/[id]?tab=surgical` | `/ortho-surgical` | Legacy list redirects to orthodontics |
| General dentistry | `/general` | None | Single specialty owner |
| Oral surgery | `/surgery` | None | Single specialty owner |
| Cephalometry | `/ceph` | None | Frozen until the Phase 12 return gate |
| Inventory | `/inventory` | None | Single active owner |
| Reports | `/reports` | `/reports/operations` is a distinct report view | No competing owner |
| Staff schedules | `/schedule` | Doctor schedule settings remain administrative context | Single operational owner |
| Communications | `/messages`, `/sms`, `/whatsapp` | None | Separate channels, not duplicate owners |

## Redirect Contract

The checked static aliases are:

| Source | Destination |
|---|---|
| `/clinic-queue` | `/daily-operations?tab=queue` |
| `/patient-journey` | `/daily-operations` |
| `/users` | `/settings?tab=permissions` |
| `/settings/users` | `/settings?tab=permissions` |
| `/settings/permissions` | `/settings?tab=permissions` |
| `/ortho-surgical` | `/ortho` |
| `/hr` | `/hr/attendance` |

`/patient-journey/[patientId]` is the only checked dynamic alias. It redirects to
`/patients/[id]?focus=journey`, preserves other query parameters, URL-encodes the
identifier, and ignores an incoming `focus` value so the alias cannot change its
declared destination context.

## Verification

- Unit tests prove every static source is unique and resolves to the declared owner.
- Redirect-page tests prove each server component invokes the registered destination.
- Dynamic redirect tests cover identifier encoding, repeated query values, and the
  owner-controlled `focus=journey` parameter.
- `CORE-P1-S3` will derive sidebar and route guards from one route-role manifest.
- `CORE-P1-S4` attached and tested backend policy ownership — see
  `core-system-route-policy-ownership.md`.
