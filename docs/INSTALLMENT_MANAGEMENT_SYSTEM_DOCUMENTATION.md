# Installment Management System (IMS)
## System Requirements & Technical Specification Document

---

## 1. Project Overview

### 1.1 Project Name
**Installment Management System (IMS)**

### 1.2 Core Concept
The **Installment Management System (IMS)** is an enterprise software solution designed to record, monitor, and streamline installment-based sales operations. The system automates the creation of financing agreements, installment schedules, customer management, product referencing, payment processing, and outstanding balance tracking.

### 1.3 Main Objective
The primary objective of the system is to provide a central, structured platform for managing deferred payment transactions, mitigating financial default risks, ensuring accurate schedule calculations, and delivering real-time visibility into organization-wide accounts receivable.

### 1.4 Problem Statement
Businesses operating installment payment models frequently suffer from operational inefficiencies caused by manual tracking, inconsistent spreadsheets, human error in schedule calculations, missing payment histories, and inadequate overdue tracking. IMS resolves these issues by establishing strict workflow rules, centralized recordkeeping, and automated balance tracking.

### 1.5 Target Audience
* **Sales Staff**: Responsible for registering customers, initiating installment sales, and generating contract terms.
* **Collection Officers**: Responsible for receiving payments, issuing receipts, and following up on overdue installments.
* **Financial Managers & Supervisors**: Responsible for approving contract terms, evaluating risk reports, and monitoring total receivables.
* **System Administrators**: Responsible for managing security permissions, user roles, system configurations, and system health.
* **Quality Assurance (QA) & Development Teams**: Responsible for implementing, verifying, and maintaining the software solution.

### 1.6 Project Scope
The scope encompasses all core operations needed to run an installment sales business from customer onboarding to final contract closure. It includes user authorization, inventory item references, agreement creation, payment schedule generation, payment recording, financial reporting, and operational auditing.

---

## 2. System Objectives

The system is engineered to achieve the following operational and financial objectives:

1. **Customer Management**: Centralize customer records, identification credentials, contact information, and purchase histories.
2. **Product Catalog Reference**: Maintain a catalog of products eligible for installment financing with reference pricing.
3. **Installment Sales Processing**: Standardize the initiation and approval of deferred payment sales.
4. **Contract Lifecycle Management**: Track contract states from draft and active status through to completion or default.
5. **Installment Schedule Generation**: Automatically calculate accurate payment schedules, installment amounts, and due dates.
6. **Payment Recording & Tracking**: Accurately log payments against specific installment schedules and update remaining balances in real time.
7. **Outstanding Balance Monitoring**: Provide real-time tracking of remaining principal balances per customer and contract.
8. **Overdue Tracking & Risk Reduction**: Flag past-due installments immediately to facilitate proactive collections.
9. **Financial Data Organization**: Standardize all financial transactional logs for auditing and reporting purposes.
10. **Error Reduction**: Eliminate manual calculation errors and prevent invalid data entry via structured validation rules.

---

## 3. System Scope

### 3.1 In-Scope Functionalities
* Customer profile registration, updating, and history tracking.
* Product reference catalog management.
* Creation and management of financing contracts.
* Calculation of down payments, principal remaining, installment counts, and periodic payment amounts.
* Automatic generation of due-dated installment schedules.
* Manual entry and allocation of payments received against due installments.
* Overdue status calculation and aging tracking.
* Role-Based Access Control (RBAC) and user session management.
* Core operational and financial reports (Sales, Overdue, Collections, Customer Balances).

### 3.2 Out-of-Scope Functionalities
* Direct automated payment gateway processing (e.g., credit card processing APIs, online banking integration) — *To Be Defined (TBD) for future releases*.
* Automated SMS or WhatsApp gateway notifications — *Out-of-scope for initial release*.
* Direct integration with third-party ERP systems (e.g., SAP, Oracle) — *Out-of-scope*.
* Legal repossession or litigation workflow automation — *Out-of-scope*.

### 3.3 Current System Boundaries
* The system is designed as an internal enterprise application backed by a C# application core and a PostgreSQL database.
* Data ingestion occurs strictly through authorized internal user interfaces.

---

## 4. Main System Modules

```
+-----------------------------------------------------------------------+
|                      Installment Management System                     |
+-----------------------------------------------------------------------+
        |                  |                  |                  |
+---------------+  +---------------+  +---------------+  +---------------+
|   Customer    |  |    Product    |  |   Contract    |  |  Installment  |
|  Management   |  |  Management   |  |  Management   |  |  Management   |
+---------------+  +---------------+  +---------------+  +---------------+
        |                  |                  |                  |
+---------------+  +---------------+  +---------------+  +---------------+
|    Payment    |  |     User      |  |    Reports    |  |   Dashboard   |
|  Management   |  |  Management   |  |   Module      |  |    Module     |
+---------------+  +---------------+  +---------------+  +---------------+
```

### 4.1 Customer Management Module
* **Purpose**: Centralize customer records and maintain verification data.
* **Main Functionalities**: Create customer profiles, update contact information, record official national identification details, view active contracts, and audit payment reliability history.
* **Module Relationships**: Serves as the primary entity referenced by Contracts, Payments, and Customer Reports.

### 4.2 Product Management Module
* **Purpose**: Manage the reference catalog of items available for installment sales.
* **Main Functionalities**: Add new products, update baseline pricing, set product availability status, and view product sales history.
* **Module Relationships**: Referenced during contract creation to determine item pricing and total deal value.

### 4.3 Contract Management Module
* **Purpose**: Oversee the formal agreement between the enterprise and the customer.
* **Main Functionalities**: Draft contracts, associate customers and products, record agreed down payments, specify installment frequencies, transition contract states (Draft $\rightarrow$ Active $\rightarrow$ Completed $\rightarrow$ Defaulted), and void unauthorized agreements.
* **Module Relationships**: Connects Customer Management, Product Management, and Installment Schedule Generation.

### 4.4 Installment Management Module
* **Purpose**: Handle the logic and schedules of individual payment periodic obligations.
* **Main Functionalities**: Generate schedule itemizations, track installment statuses (Pending, Paid, Partial, Overdue), recalculate due balances, and apply overdue flags based on system date boundaries.
* **Module Relationships**: Created by Contract Management and updated by Payment Management.

### 4.5 Payment Management Module
* **Purpose**: Process and log monetary payments received from customers.
* **Main Functionalities**: Record payment receipts, allocate payment amounts to specific due installments, handle partial payments, calculate remaining contract balances, and issue payment confirmation references.
* **Module Relationships**: Updates Installment statuses, modifies Contract overall remaining balances, and feeds Financial Reports.

### 4.6 User Management Module
* **Purpose**: Control access, security, and administrative authority across the system.
* **Main Functionalities**: User account creation, password management, role assignment, privilege enforcement, and security audit logging.
* **Module Relationships**: Intersects with all modules to restrict or allow operations based on User Roles.

### 4.7 Reports Module
* **Purpose**: Provide actionable operational and financial intelligence to management.
* **Main Functionalities**: Generate collection summaries, overdue aging reports, sales performance statistics, and customer balance statements.
* **Module Relationships**: Aggregates data from Customer, Contract, Installment, and Payment modules.

### 4.8 Dashboard Module
* **Purpose**: Provide a real-time visual summary of key performance indicators (KPIs).
* **Main Functionalities**: Display metrics such as total active contracts, total overdue amount, today's collected payments, and upcoming due installments.
* **Module Relationships**: Read-only aggregate viewer consuming data from all transaction modules.

---

## 5. User Roles & Permissions

### 5.1 User Role Definitions
1. **System Administrator (Admin)**: Has unrestricted access to system configuration, user provisioning, security logs, and database parameters.
2. **Financial Manager**: Oversees financial performance, approves contract exceptions, overrides system flags (where permitted), and views all financial reports.
3. **Sales Agent**: Onboards customers, selects products, drafts contracts, and submits agreements for approval or activation.
4. **Collection Officer**: Searches active customer schedules, accepts and records payments, prints payment receipts, and views overdue schedules.
5. **Auditor (Read-Only)**: Inspects transactional logs, customer history, reports, and system records for compliance without modification privileges.

### 5.2 Roles & Permissions Matrix

| Module / Operation | Admin | Financial Manager | Sales Agent | Collection Officer | Auditor |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Manage Users & Roles** | Full Access | No Access | No Access | No Access | No Access |
| **Create / Edit Product Catalog** | Full Access | Read / Edit | Read Only | Read Only | Read Only |
| **Register / Edit Customer Profile** | Full Access | Full Access | Full Access | Read / Edit (Contact) | Read Only |
| **Create Contract Draft** | Full Access | Full Access | Full Access | No Access | Read Only |
| **Approve / Activate Contract** | Full Access | Full Access | No Access | No Access | Read Only |
| **Void / Cancel Contract** | Full Access | Full Access | No Access | No Access | Read Only |
| **Record Customer Payment** | Full Access | Full Access | No Access | Full Access | Read Only |
| **View Financial Reports** | Full Access | Full Access | Limited | Limited | Full Access |
| **View Audit Logs** | Full Access | Read Only | No Access | No Access | Full Access |

---

## 6. Functional Requirements

### 6.1 Customer Management Requirements

#### **FR-001: Customer Registration**
* **Description**: The system must allow users to register a new customer profile.
* **Actor**: Sales Agent, Financial Manager, Admin.
* **Preconditions**: User is logged in and authorized.
* **Expected Result**: A new customer profile is saved with a unique Customer ID, valid identity details, and contact information.

#### **FR-002: Customer Search & Filtering**
* **Description**: The system must allow searching for customers by National ID, Phone Number, Name, or Customer ID.
* **Actor**: All logged-in users.
* **Preconditions**: User is authenticated.
* **Expected Result**: A filtered list of matching customer records is returned promptly.

#### **FR-003: Customer Profile Modification**
* **Description**: The system must permit editing customer contact details while retaining immutable historical identity data.
* **Actor**: Sales Agent, Collection Officer, Financial Manager, Admin.
* **Preconditions**: Selected customer record exists.
* **Expected Result**: Customer profile is updated, and an audit entry is created.

---

### 6.2 Product Catalog Requirements

#### **FR-004: Add Product Reference**
* **Description**: The system must store product details including Product Name, SKU/Code, Category, and Default Base Price.
* **Actor**: Financial Manager, Admin.
* **Preconditions**: Product code must be unique.
* **Expected Result**: Product is saved to the catalog and made available for contract creation.

#### **FR-005: Update Product Details**
* **Description**: The system must allow updating baseline pricing and status of products without altering existing historical contracts.
* **Actor**: Financial Manager, Admin.
* **Preconditions**: Target product exists in the system.
* **Expected Result**: Future contract drafts use the updated pricing while existing contracts retain their agreed historical values.

---

### 6.3 Contract Management Requirements

#### **FR-006: Contract Draft Creation**
* **Description**: The system must enable drafting a contract linking a Customer, one or more Products, Total Selling Price, Down Payment, and Installment Duration.
* **Actor**: Sales Agent, Financial Manager, Admin.
* **Preconditions**: Customer and Product records are active.
* **Expected Result**: A contract is created in `Draft` status with calculated principal remaining balance.

#### **FR-007: Contract Activation**
* **Description**: The system must transition a contract from `Draft` to `Active` state upon verifying agreement terms and down payment.
* **Actor**: Financial Manager, Admin.
* **Preconditions**: Contract is in `Draft` state; down payment requirement is satisfied.
* **Expected Result**: Contract status changes to `Active`, and the definitive Installment Schedule is locked and activated.

#### **FR-008: Contract Cancellation / Voiding**
* **Description**: The system must allow authorized users to void a contract before payments are processed, recording a formal reason for voiding.
* **Actor**: Financial Manager, Admin.
* **Preconditions**: Contract has no recorded regular installment payments.
* **Expected Result**: Contract status transitions to `Voided`, releasing reserved items and deactivating schedules.

---

### 6.4 Installment Schedule Requirements

#### **FR-009: Installment Schedule Generation**
* **Description**: Upon contract specification, the system must generate individual installment records containing Installment Number, Due Date, Principal Portion, Interest/Fee Portion (if applicable - TBD), and Expected Installment Amount.
* **Actor**: System (Automated).
* **Preconditions**: Contract parameters (Total Amount, Down Payment, Duration, Frequency) are valid.
* **Expected Result**: Exact $N$ installment schedule items are generated linked to the contract.

#### **FR-010: Overdue Status Calculation**
* **Description**: The system must evaluate due dates against the current date and automatically mark unpaid installments whose due date has passed as `Overdue`.
* **Actor**: System (Automated batch / System process).
* **Preconditions**: System clock is accurate; installment is in `Pending` state past its due date.
* **Expected Result**: Installment status changes from `Pending` to `Overdue`.

---

### 6.5 Payment Processing Requirements

#### **FR-011: Payment Entry**
* **Description**: The system must accept and record customer payments targeting specific contracts and installments.
* **Actor**: Collection Officer, Financial Manager, Admin.
* **Preconditions**: Target contract is `Active`.
* **Expected Result**: Payment record is created with timestamp, amount, payment method reference, and collecting user ID.

#### **FR-012: Payment Allocation & Balance Update**
* **Description**: Upon payment entry, the system must apply the payment amount to the earliest unpaid or overdue installment.
* **Actor**: System (Automated processing upon FR-011 execution).
* **Preconditions**: Payment record successfully created.
* **Expected Result**: Targeted installment status updates (`Paid` or `Partially Paid`), and the total contract remaining balance decreases by the exact payment amount.

#### **FR-013: Contract Completion Trigger**
* **Description**: When the cumulative payments equal the total contract balance, the system must automatically close the contract.
* **Actor**: System (Automated).
* **Preconditions**: Contract remaining balance reaches $0.00$.
* **Expected Result**: Contract status transitions automatically from `Active` to `Completed`.

---

### 6.6 User Management Requirements

#### **FR-014: User Authentication**
* **Description**: Users must log in using a unique Username and Password combination.
* **Actor**: All System Users.
* **Preconditions**: User account is active in the database.
* **Expected Result**: Secure session token generated upon valid credentials; login rejected on invalid credentials.

#### **FR-015: Role-Based Authorization Enforcement**
* **Description**: The system must verify the logged-in user's role before executing any system command or feature access.
* **Actor**: System (Automated middleware/interceptor).
* **Preconditions**: Session is active.
* **Expected Result**: Execution allowed if authorized; HTTP 403 / Access Denied error returned if unauthorized.

---

## 7. Non-Functional Requirements

### 7.1 Performance Requirements
* **Response Time**: UI query responses and database reads must complete in under 2 seconds for standard operations.
* **Transaction Execution**: Payment processing and schedule updates must complete in under 1 second per transaction.
* **Concurrency Handling**: System must support up to 50 concurrent active users without performance degradation.

### 7.2 Security Requirements
* **Password Cryptography**: Passwords must be hashed using industry-standard salted key-derivation algorithms (e.g., Argon2id or bcrypt).
* **Data Transmission Security**: All network communications between application components must use encrypted channels (TLS 1.3).
* **Session Expiration**: Inactive user sessions must automatically expire after 30 minutes of inactivity.

### 7.3 Reliability & Availability
* **Database ACID Compliance**: All database transactions affecting financial records, contracts, and payments must strictly enforce ACID guarantees.
* **Uptime Target**: The system should maintain 99.5% operational availability during core business hours.

### 7.4 Maintainability
* **Modular Code Structure**: C# code must be organized into logical, decoupled layers (Presentation, Business Logic, Data Access).
* **Clear Documentation**: Domain entities and API contracts must be explicitly documented.

### 7.5 Scalability
* **Database Design**: PostgreSQL database tables must use proper indexing on Primary Keys, Foreign Keys, and search criteria (e.g., National ID, Due Date) to support multi-year data growth.

### 7.6 Usability
* **Consistent Interfaces**: Input fields, validation warnings, error alerts, and table structures must be standardized across all user screens.
* **Feedback Indicators**: Long-running reporting queries must display visual loading state indicators.

### 7.7 Data Integrity
* **Foreign Key Constraints**: Strict relational integrity must prevent orphaned records (e.g., payments without a valid contract).
* **Monetary Precision**: All monetary values must be stored using high-precision decimal representation (e.g., PostgreSQL `NUMERIC(15, 2)`).

---

## 8. Business Workflow

```
[1. Customer Registration]
          |
          v
[2. Product Selection]
          |
          v
[3. Terms Agreement & Down Payment]
          |
          v
[4. Contract Execution (Draft -> Active)]
          |
          v
[5. Installment Schedule Generation]
          |
          +-----------------------+
          |                       |
          v                       v
[6. Period Payment Received]  [Due Date Passed - Unpaid]
          |                       |
          v                       v
[7. Allocation & Balance Update] [Marked Overdue]
          |                       |
          +-----------+-----------+
                      |
                      v
          [Remaining Balance = 0?]
             /                 \
          (Yes)                (No)
           /                     \
          v                       v
[8. Contract Completed]    [Await Next Payment]
```

### 8.1 Step-by-Step Lifecycle Explanation

1. **Customer Onboarding**: The Sales Agent creates a customer record, capturing identity documentation and contact verification.
2. **Item Selection & Terms Negotiating**: The customer selects one or more products. The agent enters proposed down payment and financing term options (e.g., 6 months, 12 months).
3. **Contract Creation & Drafting**: The system computes the proposed schedule and creates a contract in `Draft` state.
4. **Approval & Activation**: A Financial Manager verifies customer eligibility, confirms receipt of the agreed down payment, and activates the contract.
5. **Schedule Lock**: The system locks the contract parameters and generates $N$ immutable installment entries with distinct due dates.
6. **Payment Cycle Execution**:
   * On or before each due date, the customer submits a payment to a Collection Officer.
   * The Collection Officer enters the payment amount into the system.
   * The system allocates funds to the current due installment.
7. **Overdue Handling**: If a due date passes without full payment, the system automatically transitions the installment state to `Overdue`, signaling collection workflows.
8. **Contract Closure**: Once all installment rows reach `Paid` status and the contract balance reaches zero, the contract state changes to `Completed`.

---

## 9. Installment Business Logic

### 9.1 Financial Terminology & Definitions

* **Total Product Price ($P_{total}$)**: The combined retail baseline price of selected items.
* **Down Payment ($D$)**: The upfront cash amount paid by the customer at contract signing.
* **Remaining Financing Principal ($P_{rem}$)**: The portion of total price deferred for periodic payment.
  $$\text{Formula: } P_{rem} = P_{total} - D$$
* **Number of Installments ($N$)**: The total count of equal periodic payments (e.g., 6, 12, 24).
* **Installment Amount ($A_{inst}$)**: The calculated fixed amount due in each standard payment period (assuming zero-interest financing unless interest rate rules are defined - TBD).
  $$\text{Formula: } A_{inst} = \frac{P_{rem}}{N}$$
* **Due Date**: The specific calendar date by which a given installment must be paid.
* **Paid Amount ($A_{paid}$)**: The cumulative sum of payments logged against a specific installment or contract.
* **Remaining Contract Balance ($B_{rem}$)**: The total unpaid monetary obligation remaining on a contract.
  $$\text{Formula: } B_{rem} = P_{rem} - \sum A_{paid}$$
* **Overdue Days**: The number of calendar days elapsed past an installment's due date when $A_{paid} < A_{inst}$.

### 9.2 Installment Status Lifecycle
* **Pending**: The installment due date is in the future, and payment has not been fully received.
* **Paid**: The installment expected amount has been fully satisfied.
* **Partially Paid**: A payment was made, but the accumulated amount is less than the expected installment amount ($0 < A_{paid} < A_{inst}$).
* **Overdue**: The current system date exceeds the installment due date, and $A_{paid} < A_{inst}$.
* **Waived**: An authorized Financial Manager forgives a minor residual balance (Subject to approval policy - TBD).

### 9.3 General Calculation Rules (TBD Considerations)
* **Equal Installment Rule**: Standard calculation divides $P_{rem}$ by $N$. Any residual rounding cents are automatically added to the final ($N^{th}$) installment.
* **Interest & Fee Inclusion**: *To Be Defined (TBD)* — Default logic assumes 0% interest rate unless business rules specify interest or late fee calculations.

---

## 10. Data Model Documentation

*(Conceptual Data Model representation - Entity Specifications only. No SQL DDL).*

### 10.1 Entity Summary

```
+----------------+       1:N       +----------------+
|    Customer    |---------------->|    Contract    |
+----------------+                 +----------------+
                                           |
                                           | 1:N
                                           v
+----------------+       1:N       +----------------+
|    Payment     |---------------->|  Installment   |
+----------------+                 +----------------+
```

### 10.2 Entity Specifications

#### **1. User Entity**
* **Purpose**: Stores system user credentials, security metadata, and assigned roles.
* **Key Attributes**: User ID, Username, Password Hash, Full Name, Email, Role ID, Active Status, Created Timestamp.
* **Relationships**: 1-to-Many relationship with Payments (as creator) and Contracts (as creator/approver).

#### **2. Customer Entity**
* **Purpose**: Holds customer identity and contact attributes.
* **Key Attributes**: Customer ID, National ID Number, Full Name, Phone Number, Secondary Phone, Address, Registration Date, Status.
* **Relationships**: 1-to-Many relationship with Contracts.

#### **3. Product Entity**
* **Purpose**: Stores reference catalog item information.
* **Key Attributes**: Product ID, Product Code/SKU, Name, Category, Baseline Price, Active Flag.
* **Relationships**: Many-to-Many relationship with Contracts (via Contract Item detail entity).

#### **4. Contract Entity**
* **Purpose**: Represents the core financing agreement.
* **Key Attributes**: Contract ID, Contract Number, Customer ID, Total Price, Down Payment Amount, Remaining Principal, Installment Count, Start Date, Contract Status (Draft, Active, Completed, Voided), Created By User ID.
* **Relationships**: Belongs to one Customer; contains many Installment items; contains many Payment logs.

#### **5. Installment Entity**
* **Purpose**: Tracks individual periodic payment obligations.
* **Key Attributes**: Installment ID, Contract ID, Installment Sequence Number, Due Date, Expected Amount, Paid Amount, Status (Pending, Paid, Partial, Overdue).
* **Relationships**: Belongs to one Contract; referenced by Payment Allocation records.

#### **6. Payment Entity**
* **Purpose**: Stores transactional history of cash receipts.
* **Key Attributes**: Payment ID, Payment Reference Number, Contract ID, Installment ID, Payment Date, Amount Paid, Payment Method, Collected By User ID, Notes.
* **Relationships**: Belongs to one Contract and targets one Installment.

---

## 11. Database Design Principles

The underlying PostgreSQL storage design follows strict relational architecture guidelines:

1. **Primary Keys**: Every entity must possess a unique, immutable primary key (Surrogate Key UUID or auto-incrementing BigInt).
2. **Foreign Key Integrity**: All inter-entity relationships must be reinforced using explicit Foreign Key constraints.
3. **Normalization**: Database structures must conform to Third Normal Form (3NF) standards to prevent data redundancy and update anomalies.
4. **Referential Integrity Enforcement**: Deletion of parent entity records (e.g., Customer or Contract) with linked transaction histories is strictly prohibited (`ON DELETE RESTRICT`).
5. **Auditing & Timestamps**: Every table must record system lifecycle metadata (`created_at`, `updated_at`, `created_by`).
6. **Constraint Rules**: Attribute-level sanity checks must be enforced at the database level (e.g., `CHECK (amount >= 0)`).

---

## 12. Security Requirements

### 12.1 Authentication Standards
* All application interaction requires mandatory user login.
* Credentials authenticated against salted hashes stored in the User table.
* Plaintext passwords must never be stored, logged, or transmitted in unencrypted form.

### 12.2 Authorization & RBAC
* Access controls enforced at the API layer based on authenticated user roles.
* Privilege validation checked prior to performing any operation (Create, Read, Update, Delete).

### 12.3 Input Validation & Injection Prevention
* All user-submitted strings must undergo validation and sanitization.
* Data access layer must use parameterized queries exclusively to prevent SQL injection vulnerabilities.

### 12.4 Audit Logging
* Critical system actions (Contract Approval, Contract Voiding, Payment Overrides, User Permission Changes) must record an immutable audit log entry containing: User ID, Action Type, Timestamp, Target Record ID, and Pre/Post State values.

### 12.5 Sensitive Personal Data Protection
* Access to customer identity documents and financial figures is restricted strictly to authorized staff roles.

---

## 13. Error Handling Taxonomy

The system classifies and manages operational errors into distinct categories:

```
                      +-------------------+
                      |   System Error    |
                      +-------------------+
                                |
    +-----------------+---------+---------+-----------------+
    |                 |                   |                 |
    v                 v                   v                 v
[Validation]   [Authentication/    [Database/       [Business Logic]
 [Errors]       Authorization]      [Constraint]      [Violations]
```

### 13.1 Validation Errors
* **Description**: Caused by invalid, missing, or malformed input data (e.g., negative payment amount, invalid phone number format).
* **Handling Strategy**: Block transaction processing immediately; return specific user-friendly error messages indicating the exact field requiring correction.

### 13.2 Authentication & Authorization Errors
* **Description**: Caused by invalid login credentials or attempts to access endpoints without sufficient role permissions.
* **Handling Strategy**: Deny execution; log security security event; return HTTP 401 (Unauthorized) or HTTP 403 (Forbidden) response.

### 13.3 Database & Constraint Errors
* **Description**: Occur when a database write violates relational constraints (e.g., duplicate National ID, foreign key lookup failure).
* **Handling Strategy**: Roll back the active transaction gracefully; catch technical exception; return formatted message to client.

### 13.4 Business Logic Rule Errors
* **Description**: Occur when an action violates system state policies (e.g., attempting to record payment on a `Voided` or `Completed` contract).
* **Handling Strategy**: Reject state transition; explain business rule constraint clearly to user.

### 13.5 Unexpected / System Exceptions
* **Description**: Unhandled runtime faults (e.g., database network disconnect).
* **Handling Strategy**: Log full technical stack trace securely in server logs; present generic safe error message to user without exposing internal architecture details.

---

## 14. Validation Rules

| Field / Entity | Validation Rule | Error Message / Action | Policy Status |
| :--- | :--- | :--- | :--- |
| **Customer National ID** | Must be non-empty, unique, exact length (TBD) | "National ID already exists or is invalid." | Verified |
| **Customer Phone Number** | Must match standard numerical phone format | "Invalid phone number format." | Verified |
| **Monetary Values** | Amount must be $\ge 0.00$ | "Monetary amounts cannot be negative." | Verified |
| **Down Payment** | Must be $\ge 0.00$ and $\le$ Total Price | "Down payment must not exceed total price." | Verified |
| **Installment Count** | Integer value between $1$ and $36$ (TBD) | "Installment count out of allowed bounds." | Verified |
| **Contract Start Date** | Must not be a historical date prior to company setup | "Invalid contract date." | Verified |
| **Payment Amount** | Must be $> 0.00$ and $\le$ Remaining Balance | "Payment amount exceeds total remaining balance." | Verified |
| **Late Fee Rate** | Rules for calculating late payment penalties | Policy under **To Be Defined (TBD)** | TBD |
| **Early Payoff Discount** | Discount percentage for settling contracts early | Policy under **To Be Defined (TBD)** | TBD |

---

## 15. Reporting Specifications

### 15.1 Customer Portfolio Report
* **Purpose**: Summarizes active customers, total contracts per customer, and overall reliability metrics.
* **Key Data Points**: Customer Name, National ID, Phone, Active Contracts Count, Total Obligation, Total Paid.

### 15.2 Master Installment Schedule Report
* **Purpose**: Displays list of all installments due within a target date range for collection forecasting.
* **Key Data Points**: Installment ID, Contract Number, Customer Name, Due Date, Expected Amount, Status.

### 15.3 Daily Collection Report
* **Purpose**: Tracks cash inflows collected during a specific operational day or shift.
* **Key Data Points**: Payment Reference, Receipt Date, Contract Number, Customer Name, Amount Received, Payment Method, Cashier/Officer Name.

### 15.4 Outstanding Balance Summary Report
* **Purpose**: Reports the total accounts receivable balance across all active accounts for financial management.
* **Key Data Points**: Total Financed Principal, Total Collected to Date, Total Outstanding Principal Balance.

### 15.5 Overdue Aging Report
* **Purpose**: Lists past-due installments categorized by aging buckets (1-30 days, 31-60 days, 61-90 days, 90+ days).
* **Key Data Points**: Customer Name, Contract Number, Overdue Amount, Days Past Due, Collection Status.

---

## 16. System Architecture (Conceptual)

The system relies on a clean, multi-layered architectural model separating concerns across distinct system layers:

```
+-------------------------------------------------------------+
|              Presentation Layer (User Interface)            |
+-------------------------------------------------------------+
                              |  (Commands / View Models)
                              v
+-------------------------------------------------------------+
|              Application Layer (Services & DTOs)             |
+-------------------------------------------------------------+
                              |  (Domain Operations)
                              v
+-------------------------------------------------------------+
|           Business Logic / Domain Layer (Entities)          |
+-------------------------------------------------------------+
                              |  (Repositories / Persistence)
                              v
+-------------------------------------------------------------+
|              Data Access Layer (ORM / Data Services)        |
+-------------------------------------------------------------+
                              |  (SQL Queries / Connections)
                              v
+-------------------------------------------------------------+
|               PostgreSQL Database Management                |
+-------------------------------------------------------------+
```

1. **Presentation Layer**: Handles user interaction, input rendering, and presentation logic.
2. **Application Layer**: Orchestrates execution flow, manages transaction boundaries, transforms entities to Data Transfer Objects (DTOs).
3. **Business Logic Layer**: Encapsulates core calculations, installment schedule generation logic, contract state machine transitions, and validation rules.
4. **Data Access Layer**: Provides abstraction over database operations, managing data mapping and query execution against storage.
5. **PostgreSQL Database**: Persistent relational storage engine enforcing constraints, indexes, and transactional durability.

---

## 17. Technology Stack

| Technology | Role / Purpose | Status / Notes |
| :--- | :--- | :--- |
| **C#** | Primary Application Programming Language | Core Language Platform |
| **PostgreSQL** | Relational Database Management System | Persistence & Transaction Engine |
| **Git** | Distributed Version Control System | Source Code Versioning |
| **GitHub** | Code Collaboration Platform | Repository Hosting & PR Workflows |
| *C# Framework (e.g., .NET Core / WPF / ASP.NET)* | Application Framework | *To Be Defined (TBD) by Development Team* |
| *ORM Tool (e.g., EF Core / Dapper)* | Data Access Technology | *To Be Defined (TBD) by Development Team* |

---

## 18. Development Team Workflow

To ensure seamless teamwork, maintain code quality, and standardize releases, the development team adheres to the following workflow:

```
[Main Branch]  ---------------------------------------------------> (Production Ready)
                     \                                   /
[Develop Branch]  ----\---------------------------------/---------> (Integration)
                       \                               /
[Feature Branch]        \---> [Commits] --> [Pull Request]
```

### 18.1 Repository Structure & Branching Strategy
* `main`: Contains production-ready code. Direct commits are strictly forbidden.
* `develop`: Serves as the primary integration branch for ongoing development.
* `feature/*`: Dedicated branches created off `develop` for implementing specific functional requirements (e.g., `feature/fr-001-customer-reg`).
* `bugfix/*`: Dedicated branches for resolving identified defects (e.g., `bugfix/fix-overdue-calc`).

### 18.2 Development Protocol
1. **Task Assignment**: Developer selects an assigned task from the Issue Tracker.
2. **Branch Creation**: Developer creates a new feature or bugfix branch off the updated `develop` branch.
3. **Local Implementation & Self-Test**: Developer implements functional logic and verifies behavior locally.
4. **Structured Commits**: Commits must be frequent and accompanied by clear descriptive messages referencing requirement IDs.
5. **Pull Request (PR) Submission**: Upon task completion, developer opens a Pull Request targeting `develop`.
6. **Code Review**: At least one peer developer must review the PR to verify architectural standards, security rules, and code quality.
7. **Automated Verification**: Build checks and automated unit test suites must pass clean.
8. **Merge & Deletion**: Upon approval, the PR is merged into `develop`, and the feature branch is deleted.

---

## 19. Testing Strategy

The QA and development teams employ a multi-layered testing strategy to guarantee software reliability:

```
               /  User Acceptance Testing (UAT)  \
              /-----------------------------------\
             /       Functional & E2E Testing      \
            /---------------------------------------\
           /    Integration & Database Tests         \
          /-------------------------------------------\
         /            Unit Testing (Base)              \
        +-----------------------------------------------+
```

### 19.1 Unit Testing
* **Focus**: Test individual domain methods, math calculation algorithms, and validation utilities in isolation.
* **Target Areas**: Installment amount calculations, down payment validation rules, state machine transitions.

### 19.2 Integration Testing
* **Focus**: Verify integration between Data Access components and the PostgreSQL database.
* **Target Areas**: Repository query accuracy, entity mapping, transaction rollback behavior.

### 19.3 Functional & E2E System Testing
* **Focus**: Verify end-to-end user workflows against functional requirements.
* **Target Areas**: Complete lifecycle execution from customer onboarding through contract completion.

### 19.4 Security & Authorization Testing
* **Focus**: Ensure access controls cannot be bypassed.
* **Target Areas**: Attempting unauthorized operations with lower-privilege role accounts; SQL injection vulnerability checks.

### 19.5 User Acceptance Testing (UAT)
* **Focus**: Validate business workflows with stakeholders and business domain managers to ensure software aligns with operational expectations.

---

## 20. Deployment Documentation

### 20.1 Deployment Environments
* **Development (DEV)**: Local developer workstations and shared dev database for active feature building.
* **Staging / QA (STG)**: Mirror of production environment used by QA team for integration verification.
* **Production (PROD)**: Live operational environment accessible exclusively to authorized business users.

### 20.2 Prerequisites & Configuration Management
* Target servers must meet minimum hardware specifications (CPU, RAM, Disk Space).
* PostgreSQL service installed, configured, and hardened.
* System configurations (connection strings, session parameters, log levels) must be managed using Environment Variables rather than hardcoded configurations.

### 20.3 Deployment Execution Sequence (Conceptual Overview)
1. **Pre-Deployment Backup**: Execute full PostgreSQL database snapshot.
2. **Database Migration**: Apply structural schema updates sequentially.
3. **Application Core Update**: Deploy compiled C# application packages.
4. **Configuration Injection**: Apply environment-specific variables.
5. **Post-Deployment Verification**: Execute smoke test checklist to confirm system health.

---

## 21. Backup & Recovery Strategy

### 21.1 Database Backup Strategy
* **Full Backups**: Executed automatically on a daily basis during non-peak operating hours.
* **Incremental / Transaction Log Backups**: Executed hourly to ensure minimal data loss risk.
* **Storage Location**: Backups must be stored in secure secondary physical or cloud storage isolated from the primary database server.
* **Retention Policy**: Daily backups retained for 90 days; monthly snapshots retained for 1 year.

### 21.2 Recovery Objectives
* **Recovery Point Objective (RPO)**: Maximum acceptable data loss period set to $\le 1$ hour.
* **Recovery Time Objective (RTO)**: Maximum acceptable system restoration downtime set to $\le 4$ hours.

### 21.3 Disaster Recovery Considerations
* Periodic restoration drills must be performed quarterly on Staging environments to verify backup image integrity and recovery procedure documentation.

---

## 22. Future Improvements Roadmap

To preserve clear project scope boundaries, system features are categorized into current deliverables, planned enhancements, and long-term research concepts.

### 22.1 Current System Scope (Current Release)
* Core customer, product, contract, installment, and payment tracking.
* Manual payment processing and allocation.
* Role-based security and standard reporting.

### 22.2 Planned Near-Term Features (Phase 2 - Planned)
* Export reports to PDF and Excel (XLSX) formats.
* Custom installment schedules (e.g., bi-weekly, custom seasonal pay structures).
* Automated email alert summaries for collection managers.

### 22.3 Future Ideas (Long-Term / Out-of-Scope)
* Online Customer Portal for viewing personal balances and payment histories.
* SMS Gateway Integration for automated payment due reminders.
* Payment Gateway Integration for online credit card down payments.

---

## 23. Glossary of Terms

* **Authentication**: The process of verifying the identity of a user attempting to log into the system.
* **Authorization**: The process of verifying whether a logged-in user possesses rights to perform a specific action.
* **Contract**: A legally binding financing agreement recording deferred payment terms between business and customer.
* **Customer**: The individual purchasing goods on deferred installment payment terms.
* **Down Payment**: An initial upfront cash payment made by the customer at the time of contract execution.
* **Due Date**: The calendar date by which an installment payment must be satisfied.
* **Installment**: One of a series of scheduled periodic payments owed under a financing contract.
* **Out-of-Scope**: Features or requirements explicitly excluded from the current software development phase.
* **Outstanding Balance**: The remaining unpaid financial principal balance owed on a contract.
* **Overdue**: The state of an installment when its due date has passed without full payment satisfaction.
* **Principal Amount**: The total remaining purchase value being financed after deducting the down payment.
* **Role-Based Access Control (RBAC)**: A security approach restricting system access based on assigned user roles.
* **To Be Defined (TBD)**: Project items requiring further policy decision from stakeholders before finalized implementation.

---

## 24. Project Status Matrix

| Module / Component | Current Status | Notes / Category |
| :--- | :--- | :--- |
| **Project Documentation & Requirements** | **Completed** | Full Specification Finalized |
| **System Architecture Definition** | **Completed** | Multi-Layer Model Defined |
| **Data Model & Logical Dictionary** | **Completed** | Conceptual Entities Finalized |
| **Role-Based Security Model** | **Completed** | Matrix Defined |
| **Core Business Logic Rules** | **Completed** | Calculation Formulas Formatted |
| **Application Source Code Implementation** | **Planned** | Subject to Team Sprint Planning |
| **PostgreSQL Database Schema Scripts** | **Planned** | Subject to Development Execution |
| **Payment Gateway Integration** | **To Be Defined (TBD)** | Out-of-Scope for Phase 1 |
| **Late Fee Penalty Rules** | **To Be Defined (TBD)** | Requires Stakeholder Decision |

---

## 25. Documentation Standards & Governance

This document serves as the single source of truth for the **Installment Management System (IMS)**.

* **Target Readers**: Software Engineers, QA Testers, System Administrators, Financial Managers, Project Managers.
* **Maintenance Guideline**: Any modification to business logic, system scope, or data attributes must be submitted as a Documentation Revision Proposal and reviewed by the development lead prior to code execution.
* **Naming Consistency**: Terms such as `Customer`, `Contract`, `Installment`, `Payment`, and `Status` must maintain consistent capitalization and semantics across all project communication, tests, and future codebase implementations.
