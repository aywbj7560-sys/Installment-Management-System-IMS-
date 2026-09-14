# Installment Management System (IMS) - Architecture

## 1. Overall Architecture
The system uses a clean, multi-layered architectural model separating concerns across distinct layers to ensure maintainability, testability, and separation of concerns.

The solution is divided into the following projects:
* **IMS.API**: The ASP.NET Core presentation layer.
* **IMS.Application**: The application logic and use cases.
* **IMS.Domain**: Core business entities and domain rules.
* **IMS.Infrastructure**: Data access, integrations, and external services.
* **IMS.Tests**: Unit and integration tests for the system.

## 2. Project Responsibilities

### IMS.Domain
* **Purpose**: Contains the core business entities, domain models, enums, and domain-specific validation rules.
* **Key Folders**: `Entities/`, `Enums/`, `Common/`
* **Dependencies**: Has no dependencies on any other projects. It is the core of the application.

### IMS.Application
* **Purpose**: Contains the application logic, use cases, interfaces, and DTOs. Orchestrates the flow of data.
* **Key Folders**: `DTOs/`, `Interfaces/`, `Services/`, `Validators/`, `Common/`
* **Dependencies**: Depends ONLY on `IMS.Domain`.

### IMS.Infrastructure
* **Purpose**: Implements the interfaces defined in the Application layer. Responsible for data persistence (PostgreSQL), Entity Framework Core contexts, and external service communications.
* **Key Folders**: `Persistence/`, `Repositories/`, `Configurations/`, `Services/`
* **Dependencies**: Depends on `IMS.Application` and `IMS.Domain`.

### IMS.API
* **Purpose**: The entry point of the application. Responsible for exposing REST APIs, handling HTTP requests, and configuring dependency injection.
* **Key Folders**: `Controllers/`, `Middleware/`, `Extensions/`
* **Dependencies**: Depends on `IMS.Application` and `IMS.Infrastructure` for dependency injection composition and development database health verification.

### IMS.Tests
* **Purpose**: Contains unit tests for domain and application logic, and integration tests for infrastructure and API.
* **Dependencies**: Depends on `IMS.Domain`, `IMS.Application`, and `IMS.Infrastructure` for mapping tests.

## 3. Dependency Direction
```
IMS.API
   ↓
IMS.Application
   ↓
IMS.Domain

IMS.Infrastructure
   ↓
IMS.Application
   ↓
IMS.Domain
```

## 4. Docker-based Development Workflow
The host machine does **NOT** require the .NET SDK to be installed. All development, restoration, building, and running of the application is isolated inside Docker containers.

* **SDK Container (`mcr.microsoft.com/dotnet/sdk:8.0`)**: Used to execute .NET CLI commands (e.g., `dotnet new`, `dotnet build`, `dotnet test`) via volume mapping.
* **Runtime Container (`mcr.microsoft.com/dotnet/aspnet:8.0`)**: Used to host the compiled application via multi-stage Docker builds.

**Commands to run without local SDK:**
* **To build and run the API:**
  ```bash
  docker-compose up -d --build
  ```
* **To run ad-hoc dotnet commands (e.g., scaffolding):**
  ```bash
  docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet [command]
  ```

## 5. What has NOT been implemented yet (Future Stages)
The current setup establishes the foundational structure only. The following components are explicitly excluded from this phase and will be implemented in future iterations:
* EF Core Migrations and database seeding.
* Authentication (JWT) and Authorization mechanisms.
* Business Modules: Customers, Products, Guarantors, Contracts, Installments, Payments, and Reports.
* Frontend User Interface and Dashboards.

## 6. Existing PostgreSQL Database / EF Core Mapping

The API uses Microsoft.EntityFrameworkCore 8.0.22 and the
Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11 provider. Only Infrastructure references
these packages directly. Domain contains plain entity classes and three status
enums with no EF dependency. EF design tooling is not required.

`database/schema.sql` is the source of truth. The 11 entities are mapped by
individual Fluent API classes in `src/IMS.Infrastructure/Configurations/`.
`src/IMS.Infrastructure/Persistence/ImsDbContext.cs` exposes their DbSets and loads
the configurations through `ApplyConfigurationsFromAssembly`.
Mappings explicitly specify the public schema, table and column names, nullability,
identity-by-default keys, column types, lengths, defaults, unique indexes, checks,
foreign keys and delete behavior. Additional indexes mirror `database/indexes.sql`.
The existing payment-allocation trigger remains database-owned.

All NUMERIC(15,2) values use decimal with explicit precision. TIMESTAMPTZ values
use DateTime with Kind=Utc; callers must supply UTC timestamps. Npgsql rejects
non-UTC DateTime values for these columns. DATE uses DateOnly.
See [Npgsql date/time handling](https://www.npgsql.org/doc/types/datetime.html).
Statuses remain VARCHAR(20). Customer and contract enums convert to their exact
names; InstallmentStatus.PartiallyPaid explicitly converts to/from "Partially Paid".

The SQL contract-to-installment foreign key always uses ON DELETE CASCADE; the
mapping follows SQL rather than the ERD's proposed status-dependent behavior.
The existing checks fix number_of_installments at 12 and constrain installment
numbers to 1..12. They do not generate or guarantee 12 child rows. Exactly 12 rows,
the mandatory guarantor at activation, and payment allocation totals remain
future business-layer requirements. No business operations are implemented here.

### Configuration and Docker networking

`Program.cs` reads `ConnectionStrings:DefaultConnection` and calls
`AddInfrastructure`, which registers ImsDbContext with UseNpgsql. Missing or invalid
configuration fails startup with a generic message; credentials are not included.
No sensitive-data logging is enabled.

Supply `ConnectionStrings__DefaultConnection` through the process environment or
a local Docker Compose `.env` file. Copy `.env.example` and replace placeholders
locally. Both Git and the Docker build context exclude local environment files.
Do not commit real credentials or print resolved Compose configuration containing
them. In Compose .env files, single-quote the value to preserve literal dollar signs.

Example only:

```dotenv
ConnectionStrings__DefaultConnection='Host=host.docker.internal;Port=5432;Database=ims_db;Username=<DATABASE_USER>;Password=<DATABASE_PASSWORD>'
IMS_API_PORT=8080
```

Inside Docker, localhost refers to the API container. Use host.docker.internal for
PostgreSQL on Windows, with the actual PostgreSQL port and an account permitted to
connect from Docker. For an existing database container on a shared Docker network,
use its service hostname instead. Compose does not create a PostgreSQL service or
initialize a database. Set IMS_API_PORT to an available port (or 0 for automatic
assignment) if Windows blocks 8080; the development API binds to host loopback.

```powershell
docker compose build
docker compose up -d
docker compose port api 8080
```

GET / still returns "IMS API is running". In Development only, GET /health/database
uses CanConnectAsync and zero-row SELECTs built from EF metadata to verify ims_db
and all 11 mapped tables/columns. It returns HTTP 200 with
`{"database":"connected"}` or HTTP 503 with `{"database":"unavailable"}`.
It reads no business rows and returns no database or credential details. Production
does not expose this endpoint.

### Schema ownership and validation

Migrations are intentionally not used at this stage. The application never calls
Migrate, EnsureCreated or EnsureDeleted, and does not create, seed or alter the
database. Deployments continue to use the existing SQL-managed schema separately.
The mapping tests compare EF metadata against a read-only copy of schema.sql and
verify status conversions without opening a database connection.
All restore, build and test commands run in mcr.microsoft.com/dotnet/sdk:8.0:

```powershell
docker run --rm --mount "type=bind,source=${PWD},target=/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 sh -ec 'dotnet restore IMS.sln; dotnet build IMS.sln --no-restore; dotnet test IMS.sln --no-build --no-restore'
```
