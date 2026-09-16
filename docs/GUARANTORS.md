# Guarantors management

Uses the existing Guarantor and ContractGuarantor entities and mappings unchanged.
database/schema.sql is the source of truth; docs/database/ERD.md sections 3.7,
3.8 and 4.2 define the profile and historical contract relationships.

## Policy decisions where documentation is silent

The existing permission matrix does **not** contain a standalone guarantor row.
The implemented policy follows the existing Customers module and contract-drafting
workflow (which already lets Admin, Financial Manager and Sales Agent register a
guarantor while creating a contract): those three roles may create/update profiles;
all five named roles may read. Collection Officer and Auditor are read-only.
This is an explicit module policy, not a claimed pre-existing guarantor matrix rule.
Unknown roles receive 403; anonymous/invalid JWT requests receive 401. Existing JWT
configuration is unchanged.

The ERD describes guarantor contact/employment profiles but
does not specify editable identity fields. As a conservative policy consistent
with Customers' historical identity protection, full_name and identification_number
must match their stored values on PUT (after trimming); changes return 409. This
applies to both linked and unlinked guarantors. IDs and created_at are database-owned.

## Endpoints

| Endpoint | Success | Description |
| --- | --- | --- |
| POST /api/guarantors | 201 + Location | Create a profile |
| GET /api/guarantors | 200 | Paginated profile DTOs |
| GET /api/guarantors/{id} | 200 | Profile and linked-contract summaries |
| PUT /api/guarantors/{id} | 200 | Replace editable profile fields, optionally change active state |

No DELETE endpoint. Missing resources return 404. No EF entities are exposed.
All read-only queries use AsNoTracking; EF operations are async with CancellationToken.

List query: page defaults to 1; pageSize defaults to 50, range 1-100. Optional
isActive=true/false filters the existing boolean field; omission returns both states.
Optional search (maximum 150 characters) matches literal substrings of full name,
identification or either phone, and exact numeric guarantor ID. Name and identity
matching are case-insensitive; database identification uniqueness is case-sensitive.
Search and active filters combine with AND. Ordering is guarantor_id ascending.
Response contains items, totalCount, page and pageSize. Invalid pagination, null
characters or invalid boolean query values return 400.

Detail returns `guarantor` and `contracts`. Each linked summary includes
contractGuarantorId, contractId, contractNumber, status, guaranteeNotes and linkedAt,
ordered by contract ID. No payment details or financial balances are added.

## Request and validation

POST/PUT accept the same DTO:

```json
{
  "fullName": "Example Guarantor",
  "identificationNumber": "G-2026-001",
  "phone": "+964 770-1234567",
  "secondaryPhone": null,
  "address": "Baghdad",
  "occupation": "Teacher",
  "workplace": "School",
  "notes": "Profile notes",
  "isActive": true
}
```

Required, nonblank: fullName (150), identificationNumber (50), phone (20),
address (text). Optional: secondaryPhone (20), occupation (100), workplace (150),
notes (text). Parenthesized numbers are schema maximum lengths. All strings reject
null characters. Required values are trimmed; optional blank strings become null.
No email field exists and none is introduced.

No country-specific phone format is documented. The module explicitly accepts
ASCII digits, spaces, parentheses, hyphens and an optional leading +, requiring at
least one digit. Alphabetic phone values and other characters return 400.
Existing Contracts validation is unchanged to avoid modifying another module.

isActive is optional: omitted/null on POST means true; omitted/null on PUT preserves
the current value, avoiding accidental reactivation. Explicit false deactivates;
true reactivates. Other editable fields are replaced on PUT, including clearing
omitted optional fields. Unknown properties (including email, createdAt, IDs and
contract relationships) are rejected with 400 rather than silently ignored.

Duplicate identification returns 409, including concurrent inserts that hit
guarantors_identification_number_key after the preliminary lookup. Required/length/
format/body failures return 400. Identity-change attempts return 409. Unexpected
database failures use the existing generic 500 handler; internal details are hidden.
Each profile save is a single atomic EF SaveChanges operation.

## Contract safety and active behavior

Writes load and modify only the guarantor profile. They never delete or change
ContractGuarantor rows, IDs, notes or timestamps, nor contracts, items, installments,
payments or balances. Deactivation is allowed with existing links and keeps those
links visible. Existing Contracts validation rejects an inactive guarantor for new
contracts; reactivation restores eligibility. Historical relationships never vanish.

Contract reads refer to the shared current guarantor profile, so edited phone/address/
employment/profile notes are visible there; the schema has no historical profile
snapshot. This does not rewrite contract terms or contract-specific guarantee notes.

## Schema fields used

- guarantors: guarantor_id, full_name, identification_number, phone, secondary_phone,
  address, occupation, workplace, notes, is_active, created_at.
- contract_guarantors (read only): contract_guarantor_id, contract_id, guarantor_id,
  notes, created_at.
- contracts (read only): contract_id, contract_number, status.

## Files

Created:
- src/IMS.Application/Guarantors/GuarantorContracts.cs
- src/IMS.Infrastructure/Guarantors/GuarantorService.cs
- src/IMS.API/Extensions/GuarantorEndpointExtensions.cs
- tests/IMS.Tests/GuarantorTests.cs
- tests/IMS.Tests/GuarantorDockerTests.cs
- docs/GUARANTORS.md

Modified: src/IMS.API/Program.cs and src/IMS.Infrastructure/DependencyInjection.cs
for endpoint/service registration only.

## Testing

GuarantorTests uses actual JWT middleware and the real guarantor service with an
isolated EF InMemory store. It covers every route for anonymous/all roles/unknown
roles, invalid bodies/queries/schema lengths/phones, creation, duplicates, immutable
identity, detail, update, not-found, deactivation/reactivation and preserving an
omitted active flag. GuarantorDockerTests exercises deployed API + PostgreSQL:
concurrent duplicate creation, persistence/defaults, search/pagination/status filters,
contract creation with a registered guarantor, linked summary, editing/deactivation
with unchanged contract/items/installments and link data, inactive rejection for new
contracts and successful reuse after reactivation. Fixtures are uniquely identified
and removed in finally; existing records are untouched. The live test class is
isolated from unrelated live fixtures to avoid incidental serializable conflicts.

Docker verification: build the API with `docker compose up -d --build`, build an SDK
image with `docker build --target build -t ims-guarantors-tests .`, and run
`dotnet test IMS.sln -c Release` in that image on the Compose network with the
workspace mounted at /source, IMS_LIVE_TESTS=1, and the running API's existing
connection and seed-admin configuration. No host SDK or schema creation is used.

### Completed verification (2026-09-16)

- Docker API build succeeded without compiler warnings.
- Full suite with live tests enabled: **131 passed, 0 failed, 0 skipped** (109
  existing tests plus 22 Guarantors cases).
- Root and database health returned 200. Authentication, Customers, Products,
  Contracts, Payments and Guarantors passed their live workflows.
- API running on http://localhost:8081; PostgreSQL healthy. Fixtures cleaned up.
- No schema/index/entity/mapping changes or migrations. No unrelated module,
  Installments management, Reports, frontend, commits or pushes.
