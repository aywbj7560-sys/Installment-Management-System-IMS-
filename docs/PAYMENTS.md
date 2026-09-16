# Payments module

## Documented rules and scope

Implementation follows the permission matrix and FR-011, FR-012 and FR-013 in
INSTALLMENT_MANAGEMENT_SYSTEM_DOCUMENTATION.md, with database/schema.sql as the
physical source of truth and database/ERD.md for the allocation relationship.
Existing entities, mappings, SQL files, indexes and triggers are unchanged.

FR-011 requires an **Active** contract. Draft, Completed, Voided and Defaulted
contracts return 409. Contracts creation continues to create Drafts; this module
does not introduce an activation endpoint or down-payment verification.

## API

| Endpoint | Behavior |
| --- | --- |
| POST /api/payments | Atomically record and allocate a receipt; 201 with Location and details DTO |
| GET /api/payments | Paginated payment summaries; 200 |
| GET /api/payments/{id} | Payment, contract/customer summary and allocations with current installment balances/statuses; 200 or 404 |
| GET /api/contracts/{contractId}/payments | Paginated history; 404 for absent contract, empty page for existing contract without receipts |

List supports `page` (default 1), `pageSize` (default 50, maximum 100),
`contractId`, `customerId` through the contract, and case-insensitive literal
`search` over payment_reference and payment_method (maximum 150 characters).
Filters combine with AND. Results sort by payment_date then payment_id ascending.
History supports the same pagination. Pages contain items, totalCount, page, pageSize.
Read queries use AsNoTracking and all database operations accept CancellationToken.
DTOs are returned rather than EF entity graphs. Detail installment/contract values
are current balances, not historical snapshots at receipt time; allocation amounts
remain the amounts of the original receipt.

```json
{
  "contractId": 1,
  "paymentReference": "PAY-2026-001",
  "paymentDate": "2026-09-16T12:00:00+03:00",
  "amount": 125.50,
  "paymentMethod": "Cash",
  "notes": "Customer receipt"
}
```

paymentDate and notes are optional. Omitted date uses the current UTC time;
provided timestamps are normalized to UTC. Infinite sentinel timestamps are
rejected. paymentReference is required, nonblank, at most 50 characters and unique;
paymentMethod is required, nonblank and at most 30 characters. The schema has no
method enum/check, so Cash, Bank Transfer and Cheque are examples rather than a
new hard-coded restriction. Strings reject null characters. Optional blank notes
become null. receivedByUserId comes only from the signed JWT subject and must
identify an active database user. Unknown request properties are rejected with
400, including client-supplied installment IDs/allocations or receiver overrides.

## Allocation, balances and statuses

FR-012 explicitly requires the earliest unpaid or overdue installment; the
implementation orders by due_date ascending, then installment_number ascending
as an explicit deterministic tie-breaker. It distributes `min(unallocated,
installment.remaining_amount)` across Pending, Partially Paid and Overdue rows
with positive balances until the payment is fully allocated. Future installments
can receive the remainder of a lump-sum payment. Paid and Waived rows are never
allocation targets; no waiver workflow is added.

Every amount uses decimal and must fit NUMERIC(15,2): positive and at most
9999999999999.99 for payments, with no fractional cents. Validation compares
precision without modifying the submitted amount; no money is rounded.
Allocations sum exactly to the receipt amount. An allocation cannot exceed an
installment's remaining amount. Overpayments and receipts with no payable
outstanding balance return 409. Existing inconsistent paid/remaining/amount
values or a contract balance differing from the sum of installment balances
return 409 instead of silently rewriting financial history.

Affected installments increase paid_amount and decrease remaining_amount by
exactly the allocated amount. `paid_amount + remaining_amount = amount` remains
true. Zero remaining becomes `Paid`; positive remaining becomes `Partially Paid`
(existing PartiallyPaid enum mapping). FR-012 applies this partial status even to
a previously Overdue target. Untouched installments preserve their statuses.
There is no overdue scheduler or new date-based status calculation.

The contract remaining_amount decreases by the receipt amount, and at zero
the contract transitions Active -> Completed (FR-013). The ERD describes
remaining_amount as financed principal; FR-012 explicitly requires reducing that
existing column as collections occur. total_amount and down_payment stay as the
original agreement terms. No interest, penalties, refunds or other modules added.

## Transaction and errors

Creation uses one explicit PostgreSQL SERIALIZABLE transaction encompassing
validation reads, receipt insertion, allocations, installment/contract updates,
detail read and commit. Two SaveChanges calls first flush the receipt, then the
allocations and balances. Any exception/cancellation rolls back; validation exits
dispose and roll back the transaction. No partial receipt or balance update is
committed. Concurrent writers cannot both spend the same balance. Serialization,
deadlock, unique-reference and FK conflicts return 409, including provider-wrapped
Postgres exceptions. There is no automatic retry: inspect the existing reference
and retry with the same reference if appropriate, never generate a new one for an
uncertain receipt. Identity sequence gaps after rollback are normal PostgreSQL
behavior and do not represent recorded payments.

| Status | Meaning |
| --- | --- |
| 200 / 201 | Successful read / creation |
| 400 | Malformed or unknown fields, invalid IDs/query/date, blank or oversized strings, nonpositive/out-of-range/fractional-cent amount |
| 401 | Missing or invalid JWT |
| 403 | Disallowed role or unavailable receiving user |
| 404 | Contract absent on create/history, or payment absent on detail |
| 409 | Ineligible contract, no payable balance, overpayment, inconsistent balances, duplicate reference or concurrent database conflict |
| 500 | Unexpected database/transaction failure; existing generic handler hides internal details |

No client allocation selection exists, so foreign-contract installment IDs,
invalid allocation sums and over-allocation requests are rejected as unknown
request fields. All generated targets are queried by the contract ID. The existing
database cross-contract allocation trigger remains an additional safeguard.

## Authorization

| Role | All three read routes | Record |
| --- | --- | --- |
| Admin | Yes | Yes |
| Financial Manager | Yes | Yes |
| Collection Officer | Yes | Yes |
| Auditor | Yes | No |
| Sales Agent | No | No |

Unknown roles are denied. Existing JWT validation and authentication are unchanged.

## Existing schema fields used

- payments: payment_id, payment_reference, contract_id, received_by_user_id,
  payment_date, amount, payment_method, notes.
- payment_allocations: payment_allocation_id, payment_id, installment_id,
  allocated_amount, created_at (database default).
- installments: installment_id, contract_id, installment_number, due_date,
  amount, paid_amount, remaining_amount, status.
- contracts: contract_id, contract_number, customer_id, remaining_amount, status.
- customers: customer_id and full_name for summary/filtering.
- users: user_id and is_active to validate the authenticated receiver.

## Files

Created: src/IMS.Application/Payments/PaymentContracts.cs,
src/IMS.Infrastructure/Payments/PaymentService.cs,
src/IMS.API/Extensions/PaymentEndpointExtensions.cs,
tests/IMS.Tests/PaymentTests.cs, tests/IMS.Tests/PaymentDockerTests.cs,
docs/PAYMENTS.md.

Modified: src/IMS.API/Program.cs (route registration),
src/IMS.Infrastructure/DependencyInjection.cs (service registration).

## Verification

PaymentTests covers actual JWT middleware for every role and each route,
malformed/unknown request fields, invalid query parameters, financial precision,
string limits and timestamp validation. PaymentDockerTests uses the deployed API
and PostgreSQL for partial, exact, multi-installment and final payments, persistence,
balance/status reconciliation, absent/ineligible contracts, no outstanding balance,
waived rows, inconsistent balances, overpayment, duplicate reference, list/detail/
history, ordering and concurrent submissions. A SaveChanges interceptor throws
after allocation and balance writes have reached PostgreSQL, before commit;
receipt, allocations, installment changes and contract changes must all roll back.
Active fixtures are created directly for tests because activation is outside scope.
All fixtures are uniquely tagged and cleaned in finally. The Payments live class
is isolated from other live classes to prevent incidental SERIALIZABLE predicate
conflicts across fixtures; its competing-payment test runs concurrent requests.

Run the full solution in the Docker SDK image (`docker build --target build -t
ims-payments-tests .`), mounted at /source on the Compose network with
IMS_LIVE_TESTS=1 and the existing API's connection and seed-admin environment.
Command inside the SDK container: `dotnet test IMS.sln -c Release`.
Live tests explicitly skip if IMS_LIVE_TESTS is absent; final verification must
enable them. No host SDK, EnsureCreated, schema creation or migrations are used.

### Completed verification (2026-09-16)

- Docker API build succeeded without compiler warnings.
- Full solution: **109 passed, 0 failed, 0 skipped**, including all 89 existing
  tests and 20 Payments cases; IMS_LIVE_TESTS=1 enabled.
- Live receipt/allocation persistence, forced post-write rollback, exact decimal
  reconciliation and competing-payment rejection passed.
- GET / and GET /health/database returned 200; login/me, Customers, Products and
  Contracts live regressions passed.
- API is running at http://localhost:8081; PostgreSQL container is healthy.
- Test fixtures were removed. No migrations, schema/index/entity/mapping changes,
  unrelated modules, commits or pushes were made.
