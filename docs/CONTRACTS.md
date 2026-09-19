# Contracts module

## API and permissions

- POST /api/contracts: creates a Draft, its items, mandatory guarantor relation, and exactly twelve installments. Admin, Financial Manager, Sales Agent (permission matrix / FR-006).
- GET /api/contracts: all five existing roles; pagination (page default 1, pageSize default 50, maximum 100), search (case-insensitive literal contract number/customer name, maximum 150 characters), customerId, status. Filters combine with AND. Deterministic contract-ID order. Response: items, totalCount, page, pageSize.
- GET /api/contracts/{id}: all five roles; contract/customer summary, locked item prices with product summaries, guarantor profiles, and ordered installment schedule.
- POST /api/contracts/{id}/activate: changes a valid Draft to Active and returns updated details. Admin and Financial Manager only.

There is no general PUT/status endpoint. Activation is a dedicated operation; Voided and Defaulted workflows remain out of scope. Existing JWT validation and role policies remain intact; createdByUserId comes from the signed subject claim and must identify an active existing user. Collection Officer reads support the documented customer schedule lookup responsibility; it cannot create contracts or activate them.

## Draft activation

`POST /api/contracts/{id}/activate` accepts `downPaymentConfirmed` (required true), optional `reference` (maximum 100 characters), and optional `note`. Unknown fields are rejected. The authenticated JWT subject supplies the activating user and the server supplies the UTC timestamp; clients cannot supply status, user, amount, or activation time.

Activation locks the contract row in a database transaction. Only Draft may transition to Active. Active returns 409 as already active; Completed, Voided, and Defaulted return 409 as invalid source states. Two concurrent attempts cannot both succeed: the row lock serializes them, and a unique audit action/target constraint provides a second safeguard. Repeated activation is a conflict, not an idempotent success.

Before changing status, the service requires the customer, the single linked guarantor, and every referenced product to still exist and be active. It validates at least one internally consistent item, consistent total/down-payment/remaining amounts, and exactly twelve Pending installments numbered 1 through 12 whose positive amounts and remaining balances reconcile exactly to the contract remaining amount. Any eligibility or aggregate-integrity failure returns 409.

The existing schedule is validated and preserved. Activation changes only `contracts.status` from Draft to Active. It does not change contract dates or financial terms, rebuild installments, shift due dates, modify items/prices or guarantor links, or create a Payment/PaymentAllocation for the down payment. `downPaymentConfirmed` records explicit confirmation for this operation; down payment remains a contract pricing term reducing financed principal.

The same transaction inserts exactly one append-only `audit_logs` row with action `ContractActivated`, target type `Contract`, contract ID, activating user, server UTC timestamp, Draft/Active states and optional reference/note. A failure rolls back both status and audit writes. No public audit CRUD API exists.

## Request example

```json
{
  "contractNumber": "CNT-2026-001",
  "customerId": 1,
  "contractDate": "2026-09-15T00:00:00Z",
  "downPayment": 10.00,
  "items": [
    { "productId": 1, "quantity": 2, "unitPrice": 25.00 },
    { "productId": 2, "quantity": 1, "unitPrice": 60.00 }
  ],
  "guarantor": {
    "fullName": "Example Guarantor",
    "identificationNumber": "EXAMPLE-G001",
    "phone": "07701234567",
    "address": "Baghdad"
  },
  "guaranteeNotes": "Contract-specific notes"
}
```

Alternatively replace guarantor with guarantorId to associate an existing active guarantor. Exactly one choice is required. Optional new-guarantor fields: secondaryPhone, occupation, workplace, notes. Existing guarantor profiles are never overwritten; duplicate identification returns 409 with instruction to use the existing ID. New guarantors are active. This stage creates one mandatory relation per contract; the schema's existing many-to-many design is preserved without standalone guarantor endpoints.

## Schema fields used

- contracts: contract_id, contract_number, customer_id, created_by_user_id, contract_date, total_amount, down_payment, remaining_amount, number_of_installments, status, created_at.
- contract_items: contract_item_id, contract_id, product_id, quantity, unit_price, subtotal.
- guarantors: guarantor_id, full_name, identification_number, phone, secondary_phone, address, occupation, workplace, notes, is_active, created_at.
- contract_guarantors: contract_guarantor_id, contract_id, guarantor_id, notes, created_at.
- installments: installment_id, contract_id, installment_number, due_date, amount, paid_amount, remaining_amount, status.
- audit_logs: audit_log_id, user_id, action_type, target_entity_type, target_entity_id, timestamp, previous_state, new_state, reference, note.

Customer/product existence and availability use existing customer status/product is_active. Database-generated IDs and creation timestamps remain database-owned. Existing entity classes, mappings, SQL schema, and indexes are unchanged.

## Financial rules and explicit decisions

ERD defines subtotal = quantity * agreed unit_price. Unit price is explicitly supplied and locked, rather than silently selecting between the catalog's cash and optional installment reference prices. Total is the sum of subtotals. Documentation section 9 defines principal = total - down payment, zero interest/fees, and residual cents assigned to the last installment.

Exactly twelve monthly rows are generated at Draft creation as explicitly requested for this stage (the older narrative describes generation at activation). Installments 1-11 use floor(principal * 100 / 12) / 100; installment 12 is principal minus their sum. The documentation specifies last-installment residual allocation but not the rounding mode; downward cent rounding is the explicit deterministic choice. All amounts use decimal. Input monetary values with more than two fractional decimal places are rejected, never silently rounded.

The schedule sums to the financed principal, not the pre-down-payment total. Therefore sum(installments) + downPayment == totalAmount. With zero down payment, sum(installments) == totalAmount. Twelve positive amounts require principal >= 0.12; fully prepaid or smaller financed purchases cannot satisfy this schema and return 400. The example produces total 110, principal 100, eleven installments of 8.33 and a final installment of 8.37.

### Approved business rule: installment due dates

The following business rule is explicitly approved:

- The first installment is due one calendar month after the contract date.
- Installments 2 through 12 occur monthly based on the original contract day, rather than the previous installment's adjusted day.
- If a target month does not contain that day, use the last valid day of that month.

For example, a contract dated January 31, 2028 has installments due February 29, March 31, April 30, and so on through January 31, 2029. This is an approved business rule, not an implementation assumption.

The existing implementation normalizes contractDate to UTC and applies this rule to its calendar date. ContractDate is required and must allow the full schedule within supported dates. Rows start Pending, paidAmount=0, remainingAmount=amount. No overdue automation or collection is implemented for these drafts.

DownPayment is the declared contract term only: no payment receipt, collection, or allocation is created or verified here.

## Transaction and errors

The complete workflow runs inside an explicit PostgreSQL SERIALIZABLE transaction. It validates an active customer, active creator, all active products, financial totals, and guarantor. It saves contract/items/guarantor relation, then the twelve installments, reads the DTO within the transaction, and commits. Validation returns, failures, and cancellation dispose and roll back the transaction. Unique/FK/concurrent serialization/deadlock conflicts return generic 409; unknown database errors flow to the existing generic 500 handler. Serialization conflicts are not silently retried; review and retry with the same contract number. Unique contract numbers prevent duplicate retries from creating another aggregate.

400: malformed input, invalid date/query, blank/oversized strings, missing items/guarantor, duplicate product lines, nonpositive quantities/IDs, invalid precision/ranges or insufficient principal. 401: absent/invalid JWT. 403: unauthorized create role or unavailable creator. 404: missing customer/product/guarantor/contract. 409: inactive customer/product/guarantor, duplicate contract number/guarantor identification, or concurrent database conflict. 201: successful creation with Location; 200: reads.

Guarantor length limits match SQL: fullName 150, identificationNumber 50, phone/secondaryPhone 20, occupation 100, workplace 150. Required strings must be nonblank; null characters are rejected; optional whitespace becomes null. Contract number max 50. Prices, subtotals and totals fit NUMERIC(15,2); down payment cannot consume the minimum positive financed schedule. No entity graphs are exposed. Read-only queries use AsNoTracking; database operations accept cancellation tokens.

## Verification

ContractTests covers real JWT middleware roles, malformed requests/queries, nested validation and schedule reconciliation. ContractDockerTests uses the deployed API and real PostgreSQL for multi-item creation, guarantor creation/reuse, exact schedule amounts/dates, all persistence, missing/inactive dependencies, duplicates, pagination/filtering/details and immutable price snapshots. A SaveChanges interceptor deliberately fails the schedule write after the first aggregate flush to verify transaction rollback, including the new guarantor. Test data is uniquely tagged and removed in finally.

Run all tests through the Docker SDK container, with IMS_LIVE_TESTS=1 and the existing API connection/seed-admin variables on the Compose network. Without this environment live tests are explicitly skipped. No schema creation/migrations are used by tests. Existing Customers and Products live tests also exercise root, health, login/me and their own CRUD workflows.

### Completed verification (2026-09-15)

Docker rebuilt the API successfully with no compiler warnings in the final build. Full solution with IMS_LIVE_TESTS=1: 89 passed, 0 failed, 0 skipped (72 previous tests plus 17 Contracts cases). The live contract workflow and forced mid-transaction rollback passed. Root/database health, login/me, Customers and Products live regressions passed. Test records were removed. API remained running at http://localhost:8081 and PostgreSQL healthy. No migrations, schema/entity/mapping changes, payment collection, payment allocations, reports, or other unrelated module work was performed. Nothing was committed or pushed.
