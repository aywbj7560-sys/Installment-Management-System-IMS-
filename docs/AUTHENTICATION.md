# Authentication and role authorization

## Scope and architecture

Authentication uses the existing PostgreSQL `users` and `roles` tables and unchanged EF mappings. No Identity tables, migrations, schema updates, registration endpoint, or business modules were added.

- Application: login/current-user DTOs, `IAuthenticationService`, and the five existing role names.
- Infrastructure: EF user lookup, ASP.NET Core `PasswordHasher<User>`, JWT generation/settings, and development Admin seeding.
- API: dependency registration, JWT Bearer middleware, role policies, login rate limit, and endpoints.

## Local configuration

Keep real values only in the ignored `.env` file. `.env.example` documents placeholders. Docker passes these settings to the existing API container:

| Variable | Value |
| --- | --- |
| `Jwt__Key` | Cryptographically random secret, at least 32 bytes; generate locally with a standard secure random generator |
| `Jwt__Issuer` | `IMS` locally |
| `Jwt__Audience` | `IMS.API` locally |
| `Jwt__ExpirationMinutes` | `30` by default; accepted range 1–120 |
| `SeedAdmin__Email` | Local initial Admin email, or empty to disable seeding |
| `SeedAdmin__Password` | Unique password, 16–1024 characters, or empty with email to disable seeding |

Run `docker compose up -d --build`. The local API remains at `http://localhost:8081`.

The seed runs only in Development and acquires a PostgreSQL transaction advisory lock to serialize concurrent seed attempts. It checks the configured email, creates a missing user with a salted password hash, and assigns the existing Admin role. It never creates roles or changes an existing user's password, role, or active status. Changing seed credentials does not reset an existing user. Remove both seed variables after initial provisioning if desired. Production skips seeding entirely.

## Endpoints

- `POST /api/auth/login`: JSON containing `email` and `password`. Success returns `token`, `expiresAtUtc`, and a safe `user` object. Invalid credentials, unknown users, and inactive users receive the same 401 response. Login is limited to 10 attempts per minute per remote IP (429 thereafter).
  Malformed or missing JSON returns 400; an unsupported content type returns 415, including in Development. JSON property names must be double-quoted. In PowerShell, construct the request with `@{ email = $email; password = $password } | ConvertTo-Json` and send it with `Invoke-RestMethod -Method Post -ContentType 'application/json' -Body $body -Uri 'http://localhost:8081/api/auth/login'` to avoid native-command quoting errors. Keep credentials and returned tokens out of logs.
- `GET /api/auth/me`: requires `Authorization: Bearer <token>`; returns only `id`, `email`, and `role` from the validated token.
- `GET /api/auth/admin-check`: requires the Admin policy; returns `{ "authorized": true }`. An authenticated non-Admin receives 403.
- `/` and `/health/database` remain available without authentication.

Email matching is case-sensitive, matching the existing unique database constraint; surrounding input whitespace is trimmed. Passwords are never trimmed. Unsupported legacy/plain-text password values are rejected. Successful verification upgrades old supported hashes when `PasswordHasher` requests rehashing.

JWTs use HS256 with subject (user ID), email, role, issuer, audience, not-before and expiration claims. Middleware validates signature, algorithm, issuer, audience, and lifetime with no clock skew. Policies named `Admin`, `Financial Manager`, `Sales Agent`, `Collection Officer`, and `Auditor` enforce their corresponding role. User status/role changes take effect at the next login or token expiration; this stage does not include token revocation or refresh tokens. Use HTTPS when deployed beyond the existing loopback-only local environment.

No passwords, hashes, or signing keys are returned. Login responses use `Cache-Control: no-store`; server error responses are generic and EF sensitive-data logging is not enabled.

## Verification

Focused tests in `tests/IMS.Tests/AuthenticationTests.cs` cover salted password hashing, correct/incorrect password verification, successful login, inactive and unknown users, unsupported password values, JWT claims, every role policy, anonymous 401, wrong-role 403, and rejection of malformed, wrong-key, wrong-issuer, wrong-audience, and expired tokens. Existing architecture/schema mapping tests remain intact.

Build and test in the .NET 8 SDK Docker image with `dotnet test IMS.sln -c Release`; supply a temporary copy of `src`, `tests`, `database`, and `IMS.sln` to avoid altering the running database. EF InMemory is used only by unit tests; production lookup uses PostgreSQL.

Implementation references: [Microsoft JWT Bearer documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication) and [PasswordHasher verification API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1.verifyhashedpassword).
