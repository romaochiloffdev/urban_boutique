# Portfolio Submission — Urban Boutique POS

**Module:** ICE-2102 Application Development
**Student:** Ochilov Ilyosjon (ID: **B2300540**)
**Academic Year:** 2025 / 2026
**Submission date:** April 2026 (Semester 2 deliverables)

---

## Sections included in this submission

| # | Section | Artifact in this ZIP | Marks |
|---|---|---|---|
| 4 | Pseudo Code | [`PseudoCode.md`](PseudoCode.md) | 5 |
| 5 | Program Code | `UrbanBoutiqueAdmin/`, `UrbanBoutiqueCashier/`, `UrbanBoutique.Tests/` (core C# / WPF submission) | 40 |
| 6 | Test Plan | [`TestPlan.md`](TestPlan.md) + automated xUnit suite | 5 |

Sections 1–3 (Problem Statement, Problem Breakdown, Program Specification) were submitted separately in Semester 1 (December 2025).

---

## How the assessor should run the project

See [`README.md`](README.md) for the full step-by-step guide. In short:

```bash
# 1) create database
psql -U postgres -c "CREATE DATABASE urban_boutique;"

# 2) run the core C# desktop apps
dotnet run --project UrbanBoutiqueAdmin       # admin / admin123
dotnet run --project UrbanBoutiqueCashier

# 3) execute the unit tests
dotnet test UrbanBoutique.Tests/UrbanBoutique.Tests.csproj
```

No manual SQL is required beyond creating the empty database — Entity Framework Core creates and migrates the schema on first launch.

---

## Mapping to the mark scheme

### Section 4 — Pseudo Code (5 marks)

`PseudoCode.md` contains two algorithms that drive the most safety-critical parts of the system:

1. **AddToCart** — validates that the selected quantity never exceeds on-hand stock. Handles both "new item" and "existing item" paths and covers the "out of stock" edge case.
2. **CompleteCheckout** — documents the full transactional checkout including row-level locks, rollback on error, and persisting the sale record.

Written in plain English with no C#-specific syntax. Each step is numbered and edge cases are called out explicitly (`IF insufficient stock THROW ERROR`).

### Section 5 — Program Code (40 marks)

The C# submission is broken into three projects:

| Project | Role |
|---|---|
| `UrbanBoutiqueAdmin` | WPF admin dashboard — inventory, staff management, reporting |
| `UrbanBoutiqueCashier` | WPF point-of-sale terminal with transactional checkout |
| `UrbanBoutique.Tests` | xUnit unit tests (71 tests, ~1 s to run) |

**Advanced language features demonstrated** (mark scheme 31–40 band):

| Technique | Where |
|---|---|
| Events + `INotifyPropertyChanged` | `CartItemModel.Quantity` raises PropertyChanged for both the quantity and the derived subtotal |
| Delegates | WPF event handlers throughout (`Click`, `PropertyChanged`, `KeyUp`) |
| LINQ-to-Entities | All inventory / report queries (`Include`, `Where`, `Select`, `Distinct`) |
| Transactions with pessimistic locking | `NpgsqlTransaction` + `SELECT ... FOR UPDATE` in `MainWindow.BtnCheckout_Click` |
| Secure password hashing (PBKDF2) | `Data/PasswordHasher.cs`, 100 000 iterations, per-user salt |
| Exception handling | `try/catch/finally` with transaction rollback and file logging |
| Async/await | Web REST layer |
| Unit tests | 71 tests across 4 suites (see below) |
| File-based structured logging | `FileLogger` writes a rolling daily log to `%LOCALAPPDATA%\UrbanBoutique\logs` |
| Configuration externalisation | `App.config` + environment variable fallbacks so credentials are not hard-coded |

**Developer unit tests (quality assurance):**

```
UrbanBoutique.Tests/
├── PasswordHasherTests.cs          (10 tests — PBKDF2 correctness)
├── DatabaseConnectionTests.cs      (11 tests — URL parsing, fallbacks)
├── CheckoutIntegrationTests.cs     (10 tests — transactional checkout)
├── AuthValidationTests.cs          (14 tests — login / register / session)
└── AdminControllerTests.cs         (10 tests — product / category / user validation)
```

Includes both happy-path and destructive tests (malformed hashes, SQL-injection-shaped input, concurrent stock contention, invalid URIs, duplicate usernames, missing fields).

**Commenting and structure:**
- Every public type has an XML doc `<summary>` comment.
- Every non-trivial method explains *why* the approach was chosen (e.g. why `FOR UPDATE` is used, why the price is re-read from the DB at checkout).
- Consistent formatting: 4-space indentation, brace-on-new-line, `using` aliases resolved, no unused imports.

### Section 6 — Test Plan (5 marks)

[`TestPlan.md`](TestPlan.md) contains:
- **13 positive functional tests** (TC-001 … TC-013) — every requirement exercised.
- **10 negative tests** (TC-N01 … TC-N10) — invalid input is rejected gracefully.
- **5 destructive / security tests** (TC-D01 … TC-D05) — SQL injection, concurrent checkout, connection drop, unauthorised window access.

Every test lists concrete steps, the **expected outcome**, and a pass/fail column. Negative and destructive cases map directly to the "Won't" / "Must not" items in the specification.

---

## What lives in each folder

```
boutique Urban/
├── UrbanBoutiqueAdmin/        ← ★ CORE — WPF admin dashboard
├── UrbanBoutiqueCashier/      ← ★ CORE — WPF cashier terminal
├── UrbanBoutique.Tests/       ← ★ CORE — xUnit unit tests (71 tests)
├── UrbanBoutiqueWeb/          ← BONUS — ASP.NET Core web interface
├── BoutiqueUrbanPOS/          ← BONUS — static landing page
├── PseudoCode.md              ← Section 4 artifact
├── TestPlan.md                ← Section 6 artifact
├── README.md                  ← how to build and run
├── SUBMISSION.md              ← this file
└── Dockerfile + railway.json  ← bonus deployment config
```

The two `BONUS` folders extend the C# submission with a web interface (same database, same auth, same checkout logic). They are *in addition to* the required WPF core and not a replacement for it.

---

## Signed declaration

> This piece of work is the result of my own work, except where it is a group assignment for which approved collaboration has been granted. Material from the work of others (from a book, a journal or the Web) used in this assignment has been acknowledged and quotations and paraphrasing suitably indicated. I appreciate that to imply that such work is mine, could lead to a nil mark, failing the module or being excluded from the University. I also testify that no substantial part of this work has been previously submitted for assessment.

— Ochilov Ilyosjon (B2300540)
