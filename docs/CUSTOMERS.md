# Customers module

Uses the existing customers table, Customer entity, mapping, and status enum unchanged. No migrations or schema changes.

## Endpoints and permissions

All endpoints require the existing JWT Bearer authentication.

| Endpoint | Result | Roles |
| --- | --- | --- |
| POST /api/customers | 201 with customer DTO and Location | Admin, Financial Manager, Sales Agent |
| GET /api/customers | 200 paginated DTOs | All five existing roles |
| GET /api/customers/{id} | 200 customer DTO or 404 | All five existing roles |
| PUT /api/customers/{id} | 200 updated DTO or 404 | Admin, Financial Manager, Sales Agent |

Collection Officer and Auditor are read-only in this stage. The broader specification's contact-only Collection Officer editing and audit-history storage remain deferred; the existing schema has no customer audit table. No historical identity data is overwritten.

GET collection accepts optional search (maximum 150 characters), status (Active, Inactive, Blacklisted), page (default 1), and pageSize (default 50, maximum 100). Returns items, totalCount, page, pageSize ordered by customer ID. Search matches literal substrings of name, identification number, either phone, or email, and exact numeric customer ID. Name, identification, and email searches are case-insensitive. Filters combine with AND. Empty search is unfiltered; all statuses are included by default. Retrieve successive pages to get all customers.

## Request and validation

POST and PUT use the same DTO:

```json
{
  "fullName": "Example Customer",
  "identificationNumber": "EXAMPLE-001",
  "phone": "07701234567",
  "secondaryPhone": null,
  "email": "customer@example.com",
  "address": "Baghdad",
  "status": "Active"
}
```

Required nonblank fields: fullName (150), identificationNumber (50), phone (20), address (PostgreSQL text). Optional secondaryPhone (20), email (100, valid email format). Parenthesized lengths are maximums. Null characters are rejected. Required strings are trimmed; optional empty contact strings become null (email must be null or valid). Status uses exact schema names and defaults to Active when omitted. PUT replaces editable contact fields and status; send the desired status explicitly to preserve it. Name and identification must match existing values on PUT, preserving the specification's immutable identity rule. ID and createdAt are database-owned and never accepted for writes.

Identification uniqueness follows the existing case-sensitive database constraint; shared phone numbers/emails are allowed. Duplicate identification and identity-change attempts return 409; invalid input returns 400, unsupported body media types 415, absent/invalid authentication 401, disallowed roles 403. Unexpected errors use the existing generic 500 handler. Database unique violations are also handled after saving to cover concurrent creates. Async EF operations receive request cancellation tokens.

## Tests

CustomerTests exercises real JWT middleware with EF InMemory. CustomerDockerTests additionally exercises the deployed API, login/me, root, database health, PostgreSQL persistence/defaults, concurrent duplicates, status updates, and translated search. Live testing is opt-in through IMS_LIVE_TESTS=1; provide the existing API connection and seed-admin environment variables and attach the SDK container to the Compose network. It deletes only records with its generated identification number in finally. It never creates or changes schema.

Run all standard tests in the SDK Docker image with dotnet test IMS.sln. Enable the live test for Docker verification; a normal run without its environment does not perform live checks.

## Verification completed

Docker rebuilt the API successfully. Full solution test run with IMS_LIVE_TESTS=1: 43 passed, 0 failed, 0 skipped (2026-09-15). This includes all existing architecture, schema-mapping, and authentication tests. Live checks returned 200 for root, database health, login, and authenticated me; customer CRUD/search and concurrent duplicate checks passed. Test records were removed. ims_api remained running on http://localhost:8081 and ims_db healthy. No migrations, SQL schema edits, other business modules, commits, or pushes were made.
