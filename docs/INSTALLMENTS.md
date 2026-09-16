# Installments / Collections management

This module only reads the existing installment schedule. It does not create or
settle installments, allocate payments, edit balances, change statuses, or run an
overdue scheduler. Contracts and Payments remain the owners of those workflows.

## Endpoints and authorization

| GET endpoint | Result | Roles |
| --- | --- | --- |
| /api/installments | Paginated installment views | All five existing roles |
| /api/installments/{id} | One installment with contract/customer summaries | All five existing roles |
| /api/contracts/{contractId}/installments | Existing schedule ordered by installment number, then due date | All five existing roles |
| /api/collections/due | Paginated open installments due today or earlier on Active contracts | Admin, Financial Manager, Collection Officer |

Every endpoint requires the existing JWT validation. Anonymous/invalid tokens
receive 401 and unknown/disallowed roles receive 403. General read access follows
the existing Contracts module, which already exposes schedules to all five roles,
including Sales Agent. The collection queue is operational and restricted to the
documented payment/collection actors (FR-011 and role descriptions); Sales Agent
and Auditor cannot access it. Auditor retains general installment read access.
The matrix does not explicitly name these new routes; these choices derive from
existing schedule visibility and documented responsibilities.

No POST, PUT, PATCH or DELETE routes are added. No manual status management exists.
Payment allocation history is not duplicated in these DTOs; authorized payment
readers continue to use existing Payments details/history endpoints.

## Response shape

Each view contains installmentId, installmentNumber, dueDate, decimal amount,
paidAmount, remainingAmount, stored status, calculated isPastDue and isOpen,
a contract summary (contractId, contractNumber, status) and a customer summary
(customerId, fullName, phone, secondaryPhone). No EF entity graph is exposed.

List and queue return items, totalCount, page, pageSize, asOfUtcDate. Detail returns
installment and asOfUtcDate. Schedule returns contractId, items, asOfUtcDate.
Each operation captures the UTC date once, using TimeProvider. The schedule reads
all existing rows (normally twelve), without regenerating or repairing missing
rows. An existing contract with no schedule returns an empty items array; an
absent contract returns 404.

## Filtering and search

Both list and queue accept the same query parameters, combined with AND:

- contractId and customerId: positive IDs; customer is joined through contract.
- status: exact stored names Pending, Paid, Partially Paid, Overdue, Waived.
  PartiallyPaid is also accepted as the existing enum spelling; responses always
  use the schema spelling `Partially Paid`. Numeric/unknown/case-mismatched values
  are rejected.
- dueFrom and dueTo: inclusive finite dates, strictly yyyy-MM-dd.
- pastDueOnly=true: due_date before the captured UTC date and remaining_amount > 0.
- openOnly=true: remaining_amount > 0 and stored status neither Paid nor Waived.
- installmentNumber: 1-12.
- search: case-insensitive literal substring of contract number or customer name,
  up to 150 characters; blank means no search. Percent/underscore are literal.
- page: default 1, positive; pageSize: default 50, range 1-100. Offset overflow
  is rejected. False or omitted boolean flags add no restriction.

The queue always adds its Active-contract, open, and due-through-today predicates;
openOnly=false cannot disable its open restriction. Other filters can narrow it,
never broaden it. Valid combinations yielding no rows return an empty page, for
example a contract/customer mismatch or a future due range on the queue.

List/queue ordering: due_date, installment_number, contract_id, installment_id,
all ascending. In the queue this puts all calculated past-due rows before today's
rows, with stable tie-breakers across contracts. No separate priority for a stored
Overdue label overrides chronological due dates. The documentation does not define
a separate collection-queue ordering, so this is the explicit deterministic choice.

## Past-due and collection rules

FR-010 describes future automatic overdue status changes. No such automation is
implemented in the current system, and GET never performs it. Section 9 describes
past-due dates with unpaid balances. Accordingly:

`isPastDue = dueDate < asOfUtcDate && remainingAmount > 0`

Today's due date is not past due, but is included in the collection queue. Pending
and Partially Paid rows can be calculated past due while keeping their stored
statuses. Even a Waived row with a positive residual balance can have isPastDue=true
under this literal date/balance calculation, but isOpen=false and is excluded from
the queue. General views show the actual data rather than silently repairing it.
The stored status filter and pastDueOnly filter are deliberately different.

Only Active contracts enter the queue, consistent with current collection/payment
eligibility (FR-011). Draft, Completed, Voided and Defaulted remain inspectable in
general views but are excluded from this operational queue. No new recovery or
defaulted-contract payment policy is introduced. Customer status does not hide an
existing Active contract's obligations.

## Validation and errors

- 200: successful reads (including empty result sets).
- 400: nonpositive route/filter IDs, invalid pagination, invalid status/number,
  malformed boolean/numeric/date query values, reversed date range, oversized or
  null-character search, Paid/Waived requested in an open view or collection queue,
  or Paid requested with pastDueOnly=true.
- 401/403: authentication/authorization failures.
- 404: absent installment or contract; unmatched/malformed route segments follow
  existing ASP.NET route constraints.

Unexpected infrastructure errors continue through the existing generic 500 handler.

## Actual schema fields used

- installments: installment_id, contract_id, installment_number, due_date, amount,
  paid_amount, remaining_amount, status.
- contracts: contract_id, contract_number, customer_id, status.
- customers: customer_id, full_name, phone, secondary_phone.

No calculated field is persisted. Payment/PaymentAllocation entities, mappings and
logic were inspected and remain unchanged; these endpoints do not query or modify
allocation history. Monetary fields remain decimal / NUMERIC(15,2).

## Performance and read-only behavior

All queries use AsNoTracking, async EF operations and CancellationToken. DTO
projections select only required columns and join contract/customer in SQL. List
and queue issue a count query plus a page query; filtering, ordering, Skip and Take
occur before materialization. Detail uses one query; schedule uses existence plus
one ordered schedule query. There is no per-row query/N+1 behavior or SaveChanges.

Existing due_date, status, (contract_id,status), (contract_id,installment_number),
contract customer_id and primary-key indexes support the filters/joins. No indexes
were changed. Case-insensitive substring search can scan matching tables with the
existing indexes; no unverified claim of indexed substring performance is made.
Count and page are normal read-committed queries and can reflect concurrent payment
activity between queries. Each returned row reflects stored current balances;
there is no financial write or locking workflow in this module.

## Files

Created:
- src/IMS.Application/Installments/InstallmentContracts.cs
- src/IMS.Infrastructure/Installments/InstallmentService.cs
- src/IMS.API/Extensions/InstallmentEndpointExtensions.cs
- tests/IMS.Tests/InstallmentTests.cs
- tests/IMS.Tests/InstallmentDockerTests.cs
- docs/INSTALLMENTS.md

Modified: src/IMS.API/Program.cs (route registration) and
src/IMS.Infrastructure/DependencyInjection.cs (service and UTC clock registration).

## Verification

InstallmentTests uses real JWT middleware, the real read service and a fixed UTC
clock with an isolated InMemory store. Coverage includes all roles/routes, malformed
and incompatible filters, pagination, customer/contract/status/date/search filters,
exact UTC date boundaries, paid/waived exclusion, partial visibility, details,
schedules, missing IDs, absence of write routes and no tracked entities after reads.

InstallmentDockerTests uses the deployed API and real PostgreSQL. It submits a
payment through the unchanged Payments API to produce a Paid installment and a
Partially Paid installment, verifies those balances plus untouched future rows,
checks twelve-row schedules and deterministic queue ties, and verifies inactive
contract states are excluded. Before/after snapshots cover contract balances and
statuses, installments, complete receipt fields and allocation rows. GET requests
must leave snapshots identical, and existing payment details must preserve history.
Fixtures are isolated from unrelated live tests and cleaned up in finally.

Docker-only verification: `docker compose up -d --build`; build SDK target with
`docker build --target build -t ims-installments-tests .`; run
`dotnet test IMS.sln -c Release` in that image on the Compose network, mounting the
workspace at /source and passing IMS_LIVE_TESTS=1 plus the API's existing connection
and seed-admin environment. No EnsureCreated, schema generation or migrations.

### Completed verification (2026-09-16)

- Docker API build succeeded without compiler warnings.
- Full solution with live tests enabled: **171 passed, 0 failed, 0 skipped**
  (131 existing cases and 40 Installments/Collections cases).
- Root and database health returned 200. Authentication, Customers, Products,
  Contracts, Payments, Guarantors and Installments/Collections live workflows passed.
- API running at http://localhost:8081; PostgreSQL healthy. Test fixtures cleaned.
- No schema, index, entity or mapping changes; no migrations. Payments and
  allocation logic unchanged and not duplicated. No unrelated modules, scheduler,
  financial write endpoints, penalties, interest, refunds, notifications, Reports,
  frontend, commits or pushes.
