-- =====================================================
-- Database: urban_boutique
-- Full schema for the Urban Boutique POS system.
-- Quoted identifiers are used to match the EF Core mapping.
-- =====================================================

-- Run this against an empty `urban_boutique` database, OR
-- simply run the apps once — EF Core's EnsureCreated()
-- generates the same schema automatically.

BEGIN;

-- Users ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Users" (
    "UserID"   SERIAL PRIMARY KEY,
    "Username" VARCHAR(50)  NOT NULL,
    "Password" VARCHAR(255) NOT NULL,
    "Role"     VARCHAR(20)  NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username" ON "Users" ("Username");

-- Categories -------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Categories" (
    "CategoryID" SERIAL PRIMARY KEY,
    "Name"       VARCHAR(50) NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Categories_Name" ON "Categories" ("Name");

-- Products ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Products" (
    "ProductID" SERIAL PRIMARY KEY,
    "Name"      VARCHAR(100)   NOT NULL,
    "Price"     NUMERIC(18,2)  NOT NULL,
    "Category"  VARCHAR(50)
);

-- Product Variants (size / color / stock) --------------------------
CREATE TABLE IF NOT EXISTS "ProductVariants" (
    "VariantID"      SERIAL PRIMARY KEY,
    "ProductID"      INT NOT NULL REFERENCES "Products"("ProductID") ON DELETE CASCADE,
    "Size"           VARCHAR(10),
    "Color"          VARCHAR(50),
    "StockQuantity"  INT NOT NULL DEFAULT 0
);

-- Sales ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Sales" (
    "SaleID"      SERIAL PRIMARY KEY,
    "SaleDate"    TIMESTAMP NOT NULL DEFAULT NOW(),
    "TotalAmount" NUMERIC(18,2) NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS "IX_Sales_SaleDate" ON "Sales" ("SaleDate");

-- Sale Items -------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SaleItems" (
    "SaleItemID" SERIAL PRIMARY KEY,
    "SaleID"     INT NOT NULL REFERENCES "Sales"("SaleID") ON DELETE CASCADE,
    "VariantID"  INT NOT NULL REFERENCES "ProductVariants"("VariantID"),
    "Quantity"   INT NOT NULL,
    "Price"      NUMERIC(18,2) NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_SaleItems_SaleID"    ON "SaleItems" ("SaleID");
CREATE INDEX IF NOT EXISTS "IX_SaleItems_VariantID" ON "SaleItems" ("VariantID");

-- Seed default categories -----------------------------------------
INSERT INTO "Categories" ("Name") VALUES
    ('Clothing'),
    ('Footwear'),
    ('Accessories')
ON CONFLICT DO NOTHING;

COMMIT;

-- NOTE: the default admin user is created automatically by the
-- applications at first run using PBKDF2 hashing. Do NOT insert
-- users manually with plaintext passwords.
