-- =============================================================================
-- Installment Management System (IMS) - Database Performance Indexes
-- Database Engine: PostgreSQL 13+
-- Author: Development Team
-- Specification: docs/database/ERD.md
-- =============================================================================

-- -----------------------------------------------------------------------------
-- USERS INDEXES
-- -----------------------------------------------------------------------------
CREATE INDEX idx_users_role_id ON users(role_id);
CREATE INDEX idx_users_is_active ON users(is_active);

-- -----------------------------------------------------------------------------
-- CUSTOMERS INDEXES
-- -----------------------------------------------------------------------------
CREATE INDEX idx_customers_status ON customers(status);
CREATE INDEX idx_customers_phone ON customers(phone);

-- -----------------------------------------------------------------------------
-- PRODUCTS INDEXES
-- -----------------------------------------------------------------------------
CREATE INDEX idx_products_is_active ON products(is_active);

-- -----------------------------------------------------------------------------
-- GUARANTORS INDEXES
-- -----------------------------------------------------------------------------
CREATE INDEX idx_guarantors_is_active ON guarantors(is_active);
CREATE INDEX idx_guarantors_phone ON guarantors(phone);

-- -----------------------------------------------------------------------------
-- CONTRACTS INDEXES
-- -----------------------------------------------------------------------------
CREATE INDEX idx_contracts_customer_id ON contracts(customer_id);
CREATE INDEX idx_contracts_created_by_user_id ON contracts(created_by_user_id);
CREATE INDEX idx_contracts_status ON contracts(status);
CREATE INDEX idx_contracts_contract_date ON contracts(contract_date);

-- -----------------------------------------------------------------------------
-- CONTRACT_ITEMS INDEXES
-- -----------------------------------------------------------------------------
-- Note: (contract_id, product_id) is already indexed by uq_contract_items_contract_product.
-- Indexing product_id for reverse catalog lookup.
CREATE INDEX idx_contract_items_product_id ON contract_items(product_id);

-- -----------------------------------------------------------------------------
-- CONTRACT_GUARANTORS INDEXES
-- -----------------------------------------------------------------------------
-- Note: (contract_id, guarantor_id) is already indexed by uq_contract_guarantors_contract_guarantor.
-- Indexing guarantor_id for reverse guarantor contracts lookup.
CREATE INDEX idx_contract_guarantors_guarantor_id ON contract_guarantors(guarantor_id);

-- -----------------------------------------------------------------------------
-- INSTALLMENTS INDEXES
-- -----------------------------------------------------------------------------
-- Note: (contract_id, installment_number) is already indexed by uq_installments_contract_installment_number.
CREATE INDEX idx_installments_due_date ON installments(due_date);
CREATE INDEX idx_installments_status ON installments(status);
CREATE INDEX idx_installments_contract_status ON installments(contract_id, status);

-- -----------------------------------------------------------------------------
-- PAYMENTS INDEXES
-- -----------------------------------------------------------------------------
CREATE INDEX idx_payments_contract_id ON payments(contract_id);
CREATE INDEX idx_payments_received_by_user_id ON payments(received_by_user_id);
CREATE INDEX idx_payments_payment_date ON payments(payment_date);

-- -----------------------------------------------------------------------------
-- PAYMENT_ALLOCATIONS INDEXES
-- -----------------------------------------------------------------------------
-- Note: (payment_id, installment_id) is already indexed by uq_payment_allocations_payment_installment.
-- Indexing installment_id for reverse allocation lookups per installment.
CREATE INDEX idx_payment_allocations_installment_id ON payment_allocations(installment_id);
