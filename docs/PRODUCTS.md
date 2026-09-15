# Products module

## Schema and architecture

Uses the existing Product entity and products mapping unchanged. Fields are product_id, product_code, name, description, cash_price, installment_price, is_active, and created_at. There is no product status enum: availability is a boolean. Category, brand, and model are absent from the schema and are not implemented. Cash price is the required baseline price; installment price is an optional reference price, preserved as null when absent. No contract or contract-item rows are read or changed.

Application defines request/response DTOs, validation, and IProductService. Infrastructure implements async EF persistence, with AsNoTracking reads and cancellation tokens on database operations. API maps authenticated endpoints using the same pattern as Customers. Existing generic exception handling remains in place.

## Endpoints and permissions

| Endpoint | Success | Roles |
| --- | --- | --- |
| POST /api/products | 201 with DTO and Location | Admin, Financial Manager |
| GET /api/products | 200 with paginated DTOs | All five existing roles |
| GET /api/products/{id} | 200 DTO | All five existing roles |
| PUT /api/products/{id} | 200 updated DTO | Admin, Financial Manager |

FR-004 and FR-005 explicitly name Admin and Financial Manager for creation and updates. The permission matrix makes Sales Agent, Collection Officer, and Auditor read-only for products. Existing JWT validation is unchanged.

Collection query parameters: search (up to 150 characters), isActive (true/false), page (default 1), pageSize (default 50; 1-100). Search performs case-insensitive literal substring matching on code, name, and description. Percent and underscore are literal search characters. Search and availability filters combine with AND. Omitted availability includes both states. Pages are ordered by product ID and return items, totalCount, page, pageSize. Page/size arithmetic is validated against overflow.

## Requests and validation

```json
{
  "productCode": "REF-001",
  "name": "Refrigerator",
  "description": "Example reference product",
  "cashPrice": 500.00,
  "installmentPrice": 600.00,
  "isActive": true
}
```

POST and PUT use this same DTO. PUT replaces all editable fields; name and code may change. Send the desired availability explicitly on PUT because omitted isActive defaults to true. Omitted description/installmentPrice becomes null. Database-owned ID and createdAt are never assigned from the request.

- Code: required, nonblank, maximum 50 characters, unique according to the existing case-sensitive database constraint.
- Name: required, nonblank, maximum 150 characters; names need not be unique.
- Description: optional PostgreSQL text; blank becomes null.
- Strings are trimmed before persistence; null characters are rejected.
- Cash price: required; installment price: optional. Both accept 0 through 9999999999999.99 and at most two decimal places. Excess fractional precision is rejected rather than silently rounded. No invented relationship between the two prices is enforced.
- Availability: JSON boolean only, default true; null, numbers, and status strings are invalid.

Invalid input/query returns 400; unsupported content type returns 415. Missing/invalid authentication returns 401, prohibited roles 403, missing product 404, duplicate code 409. Duplicate prechecks and the PostgreSQL unique-constraint exception both map to 409, including concurrent writes. A product removed during an update returns 404. Unexpected database failures retain the existing generic 500 response, without exposing database details.

## Docker tests

ProductTests covers JWT role enforcement, create/update/read, validation, pagination, search and availability filters, duplicates, missing products, no-tracking reads, and cancellation. ProductDockerTests verifies live PostgreSQL defaults, decimal boundaries, inactive creation, update persistence, concurrent duplicate conflicts, search translation, and pagination. It uses the existing IMS_LIVE_TESTS=1 convention and removes only its generated product records in finally.

Run the full solution through the Docker SDK container with dotnet test IMS.sln. For live checks, attach it to the Compose network and supply IMS_LIVE_TESTS=1 and the existing API connection/seed-admin environment variables. The existing CustomerDockerTests also verifies root, health, authentication, and customer operations during this run. Without the live environment, live tests are explicitly skipped.

## Verified result (2026-09-15)

Docker API rebuild succeeded. Full solution run with IMS_LIVE_TESTS=1: 72 passed, 0 failed, 0 skipped, including all 43 prior tests and 29 new Products tests. Live root, database health, login/me, Customers operations, and Products operations passed. PostgreSQL persisted inactive creation and exact maximum decimal pricing correctly; concurrent duplicate creation returned one 201 and one 409. Generated test records were cleaned up. ims_api remains running at http://localhost:8081; ims_db is healthy. No migrations, schema/entity/mapping changes, other business modules, commits, or pushes were made.
