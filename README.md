# Jobnet Backend (.NET 8)

Enterprise-grade backend API for the **Jobnet** job marketplace (construction & general services across Canada). Pairs with the React + Vite frontend in `../jobnet`.

## Tech stack

| Layer        | Technology                                                          |
| ------------ | ------------------------------------------------------------------- |
| Runtime      | .NET 8 (LTS), ASP.NET Core Web API                                  |
| ORM / SQL    | Entity Framework Core 8 + **Microsoft SQL Server** 2022             |
| NoSQL        | **MongoDB** 7 (audit logs + domain events)                          |
| Auth         | JWT Bearer tokens (HS256) + BCrypt password hashing                 |
| API docs     | Swagger / OpenAPI 3 (Swashbuckle)                                   |
| Architecture | Clean / layered: Domain &rarr; Infrastructure &rarr; API            |

## Why two databases?

This split mirrors the original product spec:

- **SQL Server** is the source of truth for **transactional business data** that needs joins, constraints, and ACID guarantees: users, companies, worker profiles, jobs, applications, reviews, notifications.
- **MongoDB** is an **append-only event store** for things you don't want polluting your relational schema: audit trails of every privileged action (who suspended which user, who selected which candidate, etc.) and domain events that downstream consumers (real-time notifications, analytics, ML matching) can subscribe to later.

## Project layout

```
jobNetBackend/
├── JobNet.sln
├── docker-compose.yml               # SQL Server + MongoDB
├── README.md
└── src/
    ├── JobNet.Domain/               # POCOs only (no framework deps)
    │   ├── Entities/                # User, Company, Job, Application, Review, ...
    │   ├── Enums/                   # UserRole, JobStatus, ApplicationStatus, ...
    │   └── Documents/               # MongoDB document types (AuditLog, DomainEvent)
    │
    ├── JobNet.Infrastructure/       # EF Core, Mongo, JWT, services
    │   ├── Persistence/
    │   │   ├── JobNetDbContext.cs
    │   │   ├── Configurations/      # IEntityTypeConfiguration<> per entity
    │   │   ├── Migrations/          # EF Core migrations
    │   │   └── Seeding/             # DatabaseSeeder.cs (demo data)
    │   ├── Mongo/                   # MongoContext + settings
    │   ├── Auditing/                # IAuditLogger + MongoAuditLogger
    │   ├── Auth/                    # JwtTokenService, BcryptPasswordHasher, ICurrentUser
    │   ├── Contracts/               # Request / response DTOs
    │   ├── Mapping/                 # Entity -> DTO mapper
    │   ├── Services/                # Auth, User, Company, Worker, Job, Application,
    │   │                            # Review, Notification, AdminReports services
    │   ├── Common/                  # Result<T> pattern
    │   └── DependencyInjection.cs   # AddJobNetInfrastructure / AddJobNetJwtAuth
    │
    └── JobNet.Api/                  # HTTP layer
        ├── Controllers/             # 9 controllers covering every module
        ├── Middleware/              # Global exception handler + Result -> ActionResult
        ├── Program.cs               # Composition root, CORS, Swagger, auto-migrate
        ├── appsettings.json         # Connection strings, JWT secret, Mongo settings
        └── Properties/launchSettings.json
```

---

## Getting started

### 1. Start the databases

The easiest path is Docker:

```bash
cd jobNetBackend
docker compose up -d
```

This brings up:

- **SQL Server 2022** on `localhost:1433` (sa / `JobNet!Pass1`)
- **MongoDB 7**     on `localhost:27017`

If you already have local installs, edit `src/JobNet.Api/appsettings.json` to point at them.

### 2. Run the API

```bash
dotnet run --project src/JobNet.Api
```

On first start, the API will:

1. Apply EF Core migrations (creates the `JobNet` database).
2. Seed the demo data (only if `Users` table is empty) - the same accounts/jobs/companies as the React frontend.
3. Open Swagger UI at <http://localhost:5080/swagger>.

### 3. Demo accounts

Identical to the frontend:

| Role     | Email                         | Password      |
| -------- | ----------------------------- | ------------- |
| Admin    | `admin@jobnet.ca`             | `admin123`    |
| Employer | `david@northbuild.ca`         | `employer123` |
| Worker   | `marcus@example.com`          | `worker123`   |

POST `api/auth/login` returns a JWT - click the **Authorize** button in Swagger and paste `Bearer <token>` to call protected endpoints.

---

## Role design & permissions

Authorization is enforced via `[Authorize(Roles = ...)]` on each controller plus per-request ownership checks inside services (so an employer can't manage someone else's company, etc.).

| Role        | Capabilities                                                                                          |
| ----------- | ----------------------------------------------------------------------------------------------------- |
| `Worker`    | Manage own profile, browse jobs, apply, withdraw, review employer after being selected.              |
| `Employer`  | Manage own company, post / update / pause / close / delete jobs, shortlist / select / reject applicants, review hired workers. |
| `Admin`     | Everything an employer can do, plus suspend / reactivate users, verify companies, view platform-wide reports, re-seed demo data. |
| `Moderator` | Reserved enum slot (Phase 2).                                                                         |

JWTs include `role`, `sub` (user id), `companyId` (employers only), `email`, and `name` claims.

---

## API endpoints

All routes are prefixed `/api`. Responses use camelCase JSON; enums are serialized as strings.

### Auth

| Method | Route                          | Role        | Notes                                  |
| ------ | ------------------------------ | ----------- | -------------------------------------- |
| POST   | `auth/register`                | -           | Creates Worker or Employer + company.  |
| POST   | `auth/login`                   | -           | Returns `{ token, expiresAt, user }`.  |
| GET    | `auth/me`                      | any         | Current user info from JWT.            |
| POST   | `auth/forgot-password`         | -           | Stub. Always returns `{ ok: true }`.   |

### Users (Admin only)

| Method | Route                     | Notes                                              |
| ------ | ------------------------- | -------------------------------------------------- |
| GET    | `users?role=&query=`      | Filter + search.                                   |
| GET    | `users/{id}`              |                                                    |
| PATCH  | `users/{id}/status`       | `{ "status": "Suspended" \| "Active" \| "Pending" }` |

### Companies

| Method | Route                          | Role                  | Notes                                  |
| ------ | ------------------------------ | --------------------- | -------------------------------------- |
| GET    | `companies?query=`             | public                |                                        |
| GET    | `companies/{id}`               | public                |                                        |
| PUT    | `companies/{id}`               | Employer/Admin (owner)| Update company profile.                |
| PATCH  | `companies/{id}/verify`        | Admin                 | `{ "verified": true }`                 |

### Worker Profiles

| Method | Route                     | Role        | Notes                                       |
| ------ | ------------------------- | ----------- | ------------------------------------------- |
| GET    | `workers/{userId}`        | public      | Public profile view.                        |
| GET    | `workers/me`              | Worker      | Current worker's profile.                   |
| PUT    | `workers/me`              | Worker      | Upsert: bio, skills, certs, experience...   |

### Jobs

| Method | Route                              | Role            | Notes                                                                              |
| ------ | ---------------------------------- | --------------- | ---------------------------------------------------------------------------------- |
| GET    | `jobs?query=&category=&province=&onlyOpen=&companyId=&page=&pageSize=` | public | Paged list with filters.                          |
| GET    | `jobs/{id}`                        | public          |                                                                                    |
| POST   | `jobs`                             | Employer        | Auto-assigns to current user's company.                                            |
| PUT    | `jobs/{id}`                        | Employer/Admin  | Owner only (admin can override).                                                   |
| PATCH  | `jobs/{id}/status`                 | Employer/Admin  | `{ "status": "Open" \| "Paused" \| "Closed" \| "Filled" }`                          |
| DELETE | `jobs/{id}`                        | Employer/Admin  | Cascades to its applications.                                                      |
| GET    | `jobs/{id}/applications`           | Employer/Admin  | List applicants (owner only).                                                      |

### Applications

| Method | Route                            | Role           | Notes                                                                              |
| ------ | -------------------------------- | -------------- | ---------------------------------------------------------------------------------- |
| POST   | `applications`                   | Worker         | One per worker per job.                                                            |
| GET    | `applications/me`                | Worker         | All my applications.                                                               |
| PATCH  | `applications/{id}/status`       | varies         | Workers can only set `Withdrawn`. Employers can `Shortlisted`/`Selected`/`Rejected`. **Setting `Selected` auto-rejects all other applicants and marks the job `Filled`.** |

### Reviews

| Method | Route                                                                   | Role  | Notes                                                                  |
| ------ | ----------------------------------------------------------------------- | ----- | ---------------------------------------------------------------------- |
| POST   | `reviews`                                                               | any   | Target is either `toUserId` (worker) or `toCompanyId` (employer), not both. Only allowed for a job whose application was `Selected`. Recomputes target's aggregate rating. |
| GET    | `reviews?toCompanyId=` \| `?toUserId=` \| `?authoredBy=`                 | public| List reviews for a company / about a worker / by an author.            |

### Notifications

| Method | Route                              | Role  | Notes                                  |
| ------ | ---------------------------------- | ----- | -------------------------------------- |
| GET    | `notifications/me`                 | any   | Current user's notifications.          |
| PATCH  | `notifications/{id}/read`          | any   | Mark one as read.                      |
| POST   | `notifications/me/read-all`        | any   | Mark all as read.                      |

### Admin

| Method | Route                          | Role  | Notes                                                              |
| ------ | ------------------------------ | ----- | ------------------------------------------------------------------ |
| GET    | `admin/overview`               | Admin | Platform KPIs.                                                     |
| GET    | `admin/reports/funnel`         | Admin | Jobs by category / province + application funnel + conversion %.   |
| POST   | `admin/seed`                   | Admin | Re-seed demo dataset (only when `Users` table is empty).           |

---

## Audit & event log (MongoDB)

Every privileged business action calls `IAuditLogger.LogAsync(...)` which writes a document to the `audit_logs` collection in MongoDB. Logged actions include:

- `Auth.Login`, `Auth.LoginFailed`, `Auth.LoginBlocked`
- `User.Registered`, `User.Suspended`, `User.Reactivated`
- `Company.Updated`, `Company.Verified`, `Company.Unverified`
- `WorkerProfile.Updated`
- `Job.Created`, `Job.Updated`, `Job.Status.{Open|Paused|Closed|Filled}`, `Job.Deleted`
- `Application.Created`, `Application.Status.{Shortlisted|Selected|Rejected|Withdrawn}`
- `Review.Created`

Each entry carries the actor (`userId`, `userEmail`, `userRole`), the entity (`entityType`, `entityId`), IP, user agent, and arbitrary metadata.

In parallel, `domain_events` collects business events such as `JobPosted`, `ApplicationSubmitted`, `CandidateSelected`. These are the seed for future real-time / analytics workloads (SignalR fan-out, dashboards, recommendation engines).

If MongoDB is unreachable, audit writes degrade to local `ILogger` warnings rather than failing the SQL write - **business correctness is never sacrificed for telemetry**.

---

## EF Core migrations

The initial migration is committed at `src/JobNet.Infrastructure/Persistence/Migrations/`. It runs automatically on API start.

To create another migration after schema changes:

```bash
dotnet ef migrations add MyChange \
  --project src/JobNet.Infrastructure \
  --startup-project src/JobNet.Api \
  --output-dir Persistence/Migrations
```

To roll back the database:

```bash
dotnet ef database update 0 \
  --project src/JobNet.Infrastructure \
  --startup-project src/JobNet.Api
```

---

## Connecting the React frontend

CORS is open to any `localhost` / `127.0.0.1` origin in development, so the Vite dev server can use port `5173`, `5174`, etc. without code changes.

The frontend at `../jobnet` already calls this API:

- `src/services/api.js` - the `fetch` wrapper + JWT bearer injection.
- `src/services/endpoints.js` - one method per backend route.
- `src/contexts/AuthContext.jsx` - login/register/logout/me.
- `src/contexts/DataContext.jsx` - server-backed store with the same public surface as the previous in-memory store.

To point the frontend at a non-default API URL, drop `VITE_API_BASE_URL=http://your-host:port/api` into `jobnet/.env.local`.

---

## Configuration reference

| Setting                                  | Default                                                                | Description                          |
| ---------------------------------------- | ---------------------------------------------------------------------- | ------------------------------------ |
| `ConnectionStrings:SqlServer`            | `Server=localhost,1433;Database=JobNet;User Id=sa;Password=JobNet!Pass1;TrustServerCertificate=true;...` | SQL Server connection. |
| `Mongo:ConnectionString`                 | `mongodb://localhost:27017`                                            | Mongo URI.                           |
| `Mongo:Database`                         | `jobnet_logs`                                                          | Mongo database name.                 |
| `Jwt:Issuer` / `Jwt:Audience`            | `Jobnet` / `Jobnet.Client`                                             |                                      |
| `Jwt:SigningKey`                         | placeholder                                                            | **Must** be replaced in production.  |
| `Jwt:ExpiresMinutes`                     | `480`                                                                  | Token lifetime (8 h).                |

Override any of these via environment variables in production (e.g. `Jwt__SigningKey=...`).

---

## Verifying everything works

```bash
dotnet build                                                  # 0 warnings, 0 errors
dotnet run --project src/JobNet.Api                            # then visit /swagger
```

A quick smoke test via Swagger:

1. `POST /api/auth/login` with `david@northbuild.ca` / `employer123` -> copy the `token`.
2. Click **Authorize**, paste `Bearer <token>`.
3. `POST /api/jobs` to create a job.
4. Log out (clear Authorize), `POST /api/auth/login` as `marcus@example.com`.
5. `POST /api/applications` to apply to the new job.
6. Log back in as the employer and `PATCH /api/applications/{id}/status` with `{ "status": "Selected" }`.
7. Confirm:
   - The job's `status` is now `Filled` (GET `/api/jobs/{jobId}`).
   - Marcus has a "You have been selected!" notification (`/api/notifications/me`).
   - MongoDB's `audit_logs` shows the `Application.Status.Selected` entry and `domain_events` has a `CandidateSelected` event.
