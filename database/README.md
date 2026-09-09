# Installment Management System (IMS) - Database Deployment & Technical Documentation

---

## 1. Overview

This directory contains the official PostgreSQL relational database schema, index definitions, and seed script for the **Installment Management System (IMS)** based on the approved ERD (`docs/database/ERD.md`) and technical specifications (`docs/INSTALLMENT_MANAGEMENT_SYSTEM_DOCUMENTATION.md`).

---

## 2. Requirements

* **Database Engine**: PostgreSQL 13 or higher
* **Client Tool**: `psql` CLI, pgAdmin 4, or DBeaver
* **Encoding**: UTF-8
* **Timezone**: UTC / `TimestampTZ` aware

---

## 3. Directory & File Structure

```
database/
├── schema.sql        # Tables, Primary Keys, Foreign Keys, CHECK & UNIQUE constraints, Triggers
├── indexes.sql       # Performance B-Tree indexes for foreign keys and filter columns
├── seed_roles.sql    # Seed script for initial predefined RBAC roles
└── README.md         # Deployment instructions, execution order, and business constraint rules
```

---

## 4. Execution Order & Setup Instructions

To deploy the database schema into a clean PostgreSQL environment, execute the scripts strictly in the following sequence:

### Step 1: Create Database
```sql
CREATE DATABASE ims_db WITH ENCODING = 'UTF8';
```

### Step 2: Run DDL Scripts in Order
Using `psql` command line:

```bash
# 1. Create Tables & Constraints
psql -d ims_db -U postgres -f database/schema.sql

# 2. Create Performance Indexes
psql -d ims_db -U postgres -f database/indexes.sql

# 3. Seed System Roles
psql -d ims_db -U postgres -f database/seed_roles.sql
```

---

## 5. Dependency-Safe Table Creation Hierarchy

The schema creates tables in the following dependency-safe order:

1. `roles` (Independent)
2. `users` (Depends on `roles`)
3. `customers` (Independent)
4. `products` (Independent)
5. `guarantors` (Independent)
6. `contracts` (Depends on `customers`, `users`)
7. `contract_items` (Depends on `contracts`, `products`)
8. `contract_guarantors` (Depends on `contracts`, `guarantors`)
9. `installments` (Depends on `contracts`)
10. `payments` (Depends on `contracts`, `users`)
11. `payment_allocations` (Depends on `payments`, `installments`)

---

## 6. Business Invariants & Enforcement Matrix

### 6.1 Database-Level Enforced Invariants (PostgreSQL)

| Business Rule | Enforced By | Implementation Detail |
| :--- | :--- | :--- |
| **Fixed 12-Month Contract** | `CHECK` Constraint | `contracts.number_of_installments = 12` |
| **Monetary Precision** | `NUMERIC(15,2)` | Stored as exact fixed-point decimals without floating-point rounding errors |
| **Non-Negative Amounts** | `CHECK` Constraints | `cash_price >= 0`, `total_amount > 0`, `down_payment >= 0`, `amount > 0` |
| **Unique Business Identifiers** | `UNIQUE` Constraints | `contract_number`, `identification_number`, `product_code`, `payment_reference` |
| **Unique Composite Associations** | `UNIQUE` Indices | `(contract_id, product_id)`, `(contract_id, guarantor_id)`, `(contract_id, installment_number)`, `(payment_id, installment_id)` |
| **Controlled Status Values** | `CHECK` Constraints | `customers.status`, `contracts.status`, `installments.status` |
| **Same Contract Allocation Match** | PL/pgSQL Trigger | `trg_validate_payment_allocation_contract` verifies `payment_id` and `installment_id` belong to the same contract |
| **Zero Late Penalties / Interest** | Schema Exclusion | Excludes `penalty_amount` and `interest_amount` fields entirely |

---

### 6.2 Application-Level Transactional Envariants (C# Service Layer)

The following complex business invariants must be validated transactionally inside the C# application service layer prior to committing database changes:

1. **Mandatory Contract Guarantor Verification**:
   * *Rule*: Every contract must have at least one valid link in `contract_guarantors` before transitioning from `Draft` state to `Active` state.
   * *Reason*: PostgreSQL foreign keys enforce child-to-parent existence, but cannot enforce parent-to-child existence at `INSERT` time without circular references.

2. **Payment Allocation Sum Integrity**:
   * *Rule*: The cumulative sum of `allocated_amount` across all `payment_allocations` belonging to a payment transaction MUST exactly equal `payments.amount`.
   * *Reason*: Requires cross-row sum validation across multiple allocation rows.

3. **Installment Overpayment Prevention**:
   * *Rule*: The `allocated_amount` applied to an installment must not exceed the installment's remaining unpaid balance (`installment.remaining_amount`).

4. **Real-time Balance Synchronization**:
   * *Rule*: Upon payment allocation, the C# service layer must transactionally update `installments.paid_amount`, `installments.remaining_amount`, `installments.status`, `contracts.remaining_amount`, and trigger contract completion (`status = 'Completed'`) when `contracts.remaining_amount = 0.00`.
