-- =============================================================================
-- Installment Management System (IMS) - Seed Script: System Roles
-- Database Engine: PostgreSQL 13+
-- Author: Development Team
-- Specification: docs/INSTALLMENT_MANAGEMENT_SYSTEM_DOCUMENTATION.md Section 5
-- =============================================================================

INSERT INTO roles (role_name, description) VALUES
('Admin', 'System Administrator with full access to user provisioning, configurations, and system logs.'),
('Financial Manager', 'Oversees financial performance, contract approvals, overrides, and financial reports.'),
('Sales Agent', 'Registers customers, drafts installment sales contracts, and submits agreements for approval.'),
('Collection Officer', 'Searches customer schedules, accepts and records cash payments, and prints receipts.'),
('Auditor', 'Inspects transactional history, audit logs, and operational reports in read-only mode.')
ON CONFLICT (role_name) DO UPDATE SET description = EXCLUDED.description;
