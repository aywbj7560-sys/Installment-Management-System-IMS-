# Users Management and Role Administration

This module uses the existing `users` and `roles` tables, entities, mappings, five system role names, ASP.NET Core `PasswordHasher<User>`, and JWT authorization unchanged. It adds no schema, migration, index, entity, mapping, token, or permission-system changes.

## Endpoints and authorization

All endpoints are Admin-only. Anonymous requests receive 401 and authenticated non-Admin or unknown roles receive 403.

- `GET /api/users`: safe paginated DTOs, ordered by user ID. Optional `search` matches full name or email case-insensitively; optional exact `role` and `isActive` filters combine with it. Page size is 1-100.
- `GET /api/users/{id}`: one safe DTO or 404.
- `POST /api/users`: creates an account with `username`, `email`, `password`, `fullName`, `role`, and `isActive`.
- `PUT /api/users/{id}`: updates `username`, `email`, `fullName`, `role`, and `isActive`. IDs and creation timestamps remain database-owned. Passwords are not accepted here.
- `GET /api/roles`: returns the existing five system roles from the database in canonical order.

There are no user-delete or role-mutation endpoints. No administrative password-reset endpoint is added because the documented scope does not define one.

## Security and safety

Passwords must be 16-1024 characters and are hashed by the same salted ASP.NET Core password hasher used by login and development seeding. Passwords and hashes never appear in DTOs. Email and username uniqueness prechecks provide normal conflicts; the named PostgreSQL unique constraints remain authoritative and concurrent violations are translated to generic 409 responses.

Only the five documented role names may be assigned, and the selected role must exist in the database. An authenticated Admin cannot deactivate their own account or remove their own Admin role. The last active Admin cannot be deactivated or demoted. Other users may be deactivated and reactivated; inactive login remains rejected by the existing authentication service. Existing JWTs retain their documented behavior until expiration.

Soft deactivation leaves users and all contract/payment history intact. PostgreSQL foreign keys remain `ON DELETE RESTRICT`, and the API exposes no DELETE operation.
