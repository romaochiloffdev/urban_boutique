# Black-Box Testing Plan
**Author:** Ochilov Ilyosjon (ID: B2300540)
**Project:** Urban Style POS System

This document outlines the black-box testing plan for the Urban Style POS System. Every requirement in the Program Specification is exercised by at least one test. The plan has three sections:

1. **Positive / functional tests (TC-001 … TC-013)** — the feature works in the intended path.
2. **Negative tests (TC-N01 … TC-N10)** — invalid input is handled gracefully.
3. **Destructive / security tests (TC-D01 … TC-D05)** — attempts to abuse the system fail safely.

## Environment
- Admin WPF app running against PostgreSQL (`urban_boutique` database, seeded with the default admin).
- Cashier WPF app sharing the same database.
- Credentials: `admin` / `admin123` (default on first run).

---

## Section 1 — Positive / functional tests

| Test ID | Feature / Module | Description | Steps | Expected Outcome | Pass / Fail |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TC-001** | Login | Valid admin can log in. | 1. Open Login window.<br>2. Enter `admin` / `admin123`.<br>3. Click **SIGN IN**. | Admin dashboard opens, username shown top-right. | [ ] |
| **TC-002** | Login | Invalid credentials are rejected. | 1. Enter `admin` / `wrong`.<br>2. Click **SIGN IN**. | Inline error banner "Invalid username or password.". No dashboard opens. | [ ] |
| **TC-003** | Add Staff User | Admin adds a new Sales Staff account. | 1. Sign in as admin.<br>2. Click **STAFF MANAGEMENT**.<br>3. Fill username, password (≥4 chars), role = Sales Staff.<br>4. Click **ADD NEW USER**. | Success dialog; new user appears in the System Users grid. | [ ] |
| **TC-004** | Reset Password | Admin resets an existing user's password. | 1. Open Staff Management.<br>2. Enter the username of an existing user.<br>3. Enter a new password.<br>4. Click **RESET PASSWORD**. | Success dialog. The user can subsequently sign in with the new password. | [ ] |
| **TC-005** | Add Product | A product with one variant is saved. | 1. In the Add Product panel fill name, price, category, size, colour, stock.<br>2. Click **SAVE PRODUCT**. | Success dialog. Inventory grid refreshes and shows the new row. | [ ] |
| **TC-006** | Low-Stock Indicator | Rows with stock < 5 are highlighted. | 1. Add a product with stock = 3.<br>2. Observe the inventory grid. | The row is rendered in bold red with a tooltip "Low stock — restock soon". | [ ] |
| **TC-007** | Product Search | Search filters the product list. | 1. Open Cashier.<br>2. Type part of a product name.<br>3. Click **Search**. | The Available Products grid shows only matching rows. | [ ] |
| **TC-008** | Add to Cart | Selected product is added to the cart. | 1. Select a row in Available Products.<br>2. Click **ADD TO CART**. | The item appears in the cart with Qty = 1 and the total updates. | [ ] |
| **TC-009** | Stock Cap in Cart | Cannot add more than available stock. | 1. Pick an item with stock = 2.<br>2. Click **ADD TO CART** three times. | Third click shows "Not enough stock available!". Cart Qty stays at 2. | [ ] |
| **TC-010** | Remove from Cart | Items can be removed or decremented. | 1. Add an item twice.<br>2. Select the cart line.<br>3. Click **REMOVE SELECTED** twice. | First click: Qty → 1. Second click: line removed. Total updates accordingly. | [ ] |
| **TC-011** | Checkout | Completing a checkout persists a Sale and deducts stock. | 1. Add two items to the cart.<br>2. Click **COMPLETE CHECKOUT**. | Success dialog with a Sale ID and total. Cart clears. Stock in Available Products drops by the sold quantity. | [ ] |
| **TC-012** | Today's Revenue | Reports reflect the completed sale. | 1. Complete a $50 sale.<br>2. Open Admin → **REPORTS**. | "Today's Revenue" card shows the running total including the new sale; "Transactions" counter increments by 1. | [ ] |
| **TC-013** | Dead Stock | Unsold items with stock > 0 appear in the dead-stock report. | 1. Add a brand-new product.<br>2. Open Reports. | The new product appears in the "Dead Stock" grid. | [ ] |

---

## Section 2 — Negative tests (invalid input must fail cleanly)

| Test ID | Scenario | Steps | Expected Outcome | Pass / Fail |
| :--- | :--- | :--- | :--- | :--- |
| **TC-N01** | Empty login fields | 1. Leave username or password blank.<br>2. Click **SIGN IN**. | Inline error "Please enter both username and password.". No network request made. | [ ] |
| **TC-N02** | Negative product price | 1. Add Product with price = `-10`.<br>2. Click **SAVE**. | Warning dialog "Please enter a valid positive price.". Product is **not** saved. | [ ] |
| **TC-N03** | Non-numeric product price | 1. Add Product with price = `abc`. | Warning dialog; no DB insert. | [ ] |
| **TC-N04** | Negative stock quantity | 1. Add Product with stock = `-5`. | Warning dialog "Please enter a valid stock quantity.". | [ ] |
| **TC-N05** | Missing required fields on product | 1. Add Product with name empty. | Warning dialog; form does not submit. | [ ] |
| **TC-N06** | Short password on Add User | 1. Open Staff Management.<br>2. Enter password = `ab` (< 4 chars).<br>3. Click **ADD NEW USER**. | Warning "Password must be at least 4 characters.". No DB insert. | [ ] |
| **TC-N07** | Duplicate username | 1. Try to add a user with the same username as an existing one. | Warning "Username already exists. Use Reset to change password.". | [ ] |
| **TC-N08** | Reset password for unknown user | 1. In Staff Management enter a username that doesn't exist.<br>2. Click **RESET PASSWORD**. | Warning "User not found.". | [ ] |
| **TC-N09** | Empty cart checkout | 1. Open Cashier with empty cart.<br>2. Click **COMPLETE CHECKOUT**. | Dialog "Shopping cart is empty.". No Sale row created. | [ ] |
| **TC-N10** | Remove with nothing selected | 1. Click **REMOVE SELECTED** without selecting. | Info dialog "Please select an item to remove.". | [ ] |

---

## Section 3 — Destructive / security tests

| Test ID | Scenario | Steps | Expected Outcome | Pass / Fail |
| :--- | :--- | :--- | :--- | :--- |
| **TC-D01** | SQL injection in product search | 1. In Cashier search, enter:<br>`'; DROP TABLE "Products"; --` | No error, no matches, database intact. Parameterised queries treat the input as literal text. | [ ] |
| **TC-D02** | SQL injection in username | 1. On login, enter username `' OR 1=1 --` with any password. | Login fails with "Invalid username or password.". The admin account is not exposed. | [ ] |
| **TC-D03** | Concurrent checkout of last item | 1. Two Cashier windows open against the same DB.<br>2. Both add the last remaining unit (stock = 1) to their carts.<br>3. Both press **COMPLETE CHECKOUT** at the same time. | One sale succeeds. The other shows "Insufficient stock for … Only 0 left." and is rolled back. Final stock = 0. No overselling. | [ ] |
| **TC-D04** | Database connection dropped mid-checkout | 1. Start a checkout.<br>2. Stop the PostgreSQL service before the transaction commits. | Transaction error dialog is shown. Reconnect and verify: no Sale row was written, and stock was not deducted. | [ ] |
| **TC-D05** | Non-admin tries to reach MainWindow | 1. Manually set `CurrentUser.Role = "Sales Staff"` (e.g. by launching a Sales Staff login).<br>2. Attempt to open `MainWindow` directly. | `MainWindow` detects the wrong role in its constructor, shows "Access denied", and shuts the application down. | [ ] |

---

## How to execute

1. Reset the database (`DROP` + `CREATE DATABASE urban_boutique`) so the run starts from a known state.
2. Launch the Admin app. The first run seeds the default admin and categories.
3. Work through Sections 1, 2 and 3 in order — negative tests rely on the positive paths existing first.
4. Record a **Pass** (`[x]`) or **Fail** (`[ ] + note`) in the final column.
5. Inspect the log file at `%LOCALAPPDATA%\UrbanBoutique\logs\urban-boutique-YYYY-MM-DD.log` when diagnosing failures; every login attempt, stock deduction and rollback is recorded there.

## Automated developer tests

In addition to this black-box plan, the repository ships with **71 unit tests** in the `UrbanBoutique.Tests` project (xUnit) that cover:

- PBKDF2 hashing & verification (salt, case-sensitivity, malformed inputs, Unicode).
- `DATABASE_URL` parsing and public-URL resolution.
- Checkout transaction (valid, empty, zero/negative qty, insufficient stock, unknown variant).
- Admin validation (price, stock, name, category uniqueness, role coercion).
- Auth flow (register, login, session state, unknown user, missing fields).

Run them with:

```bash
dotnet test UrbanBoutique.Tests/UrbanBoutique.Tests.csproj
```

Expected: **71 passed, 0 failed**.
