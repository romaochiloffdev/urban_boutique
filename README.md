# Urban Boutique POS

Four-module point-of-sale system for a fashion boutique: a public web storefront, administrator dashboard, cashier terminal and desktop apps — all sharing one PostgreSQL database.

## Modules

| Folder | Purpose | Stack |
|---|---|---|
| `UrbanBoutiqueWeb/` | Web app — storefront, admin dashboard, cashier terminal, REST API | ASP.NET Core 10 + EF Core |
| `UrbanBoutiqueAdmin/` | Admin desktop — inventory, staff, reports | WPF (.NET 10) |
| `UrbanBoutiqueCashier/` | Cashier desktop — POS terminal with atomic checkout | WPF (.NET 10) |
| `BoutiqueUrbanPOS/` | Public landing page describing the system | ASP.NET Core (static) |

Only `UrbanBoutiqueWeb` is deployable to the cloud — the WPF apps are Windows-only desktop clients.

---

## Local development

Prerequisites:
- .NET 10 SDK
- PostgreSQL 14+ running on `localhost:5432`
- A database named `urban_boutique` (the app creates tables automatically)

```bash
cd UrbanBoutiqueWeb
dotnet run
```

Then open http://localhost:5000 (port shown in terminal output).

Default admin: `admin` / `admin123`

Connection string can be overridden in `appsettings.json` or via the `DATABASE_URL` environment variable.

---

## Deploy to Railway

### 1 — Create a new Railway project

1. Go to https://railway.app → **New Project** → **Deploy from GitHub repo**
2. Choose this repository (`urban_boutique`)
3. **No configuration needed** — Railway auto-detects the root `Dockerfile` and `railway.json`, which build only the `UrbanBoutiqueWeb` project. Leave *Root Directory* empty.

> If Railway tries to use its *Railpack* auto-builder and misdetects the stack (e.g. "PHP" error), this is because the Dockerfile wasn't picked up. Make sure you pulled the latest commit — the repo root now has `Dockerfile` and `railway.json` that explicitly tell Railway to use Docker.

### 2 — Add a PostgreSQL service

1. In the project dashboard → **+ New** → **Database** → **Add PostgreSQL**
2. Railway automatically creates a `DATABASE_URL` variable on the Postgres service
3. Open your web service → **Variables** → **Add Reference** → select the Postgres service → pick `DATABASE_URL`

The app auto-parses `DATABASE_URL` (format: `postgresql://user:pass@host:port/db`) into a valid Npgsql connection string and enables `SSL Mode=Require`.

### 3 — Set admin credentials (recommended)

Add these variables on the web service so the default admin password isn't `admin123` in production:

| Variable | Value |
|---|---|
| `ADMIN_USERNAME` | *(e.g. `roman`)* |
| `ADMIN_PASSWORD` | *(a strong password)* |

On first deploy the app seeds this user. If a user with `ADMIN_USERNAME` already exists but has an old-format password hash, it's automatically re-hashed with PBKDF2.

### 4 — Deploy

Railway will build the Dockerfile, provision Postgres, run migrations (via `EnsureCreated`) and start the app. A healthcheck endpoint at `/healthz` verifies readiness.

Open the generated Railway URL — you should see the storefront. Admin panel lives at `/admin`, cashier terminal at `/cashier`.

---

## Environment variables reference

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `DATABASE_URL` | ✅ on Railway | — | PostgreSQL URI (auto-provided by Railway) |
| `PORT` | no | `8080` | HTTP port (auto-provided by Railway) |
| `ADMIN_USERNAME` | no | `admin` | Seeded admin username |
| `ADMIN_PASSWORD` | no | `admin123` | Seeded admin password |
| `APP_URL` | no | auto | Public site URL (e.g. `https://urban-boutique.up.railway.app`). Used by `/api/config` so the frontend knows its canonical origin |
| `RAILWAY_PUBLIC_DOMAIN` | no | — | Auto-set by Railway. Used as fallback for `APP_URL` when not explicitly provided |
| `ASPNETCORE_ENVIRONMENT` | no | `Production` | Set to `Development` to disable forced HTTPS cookies |

Runtime config is exposed at **`GET /api/config`** (returns `{"appUrl":"...", "environment":"Production"}`), so the frontend can reach the public URL without hard-coding it.

For local development you can still use `ConnectionStrings:DefaultConnection` in `appsettings.json` — `DATABASE_URL` takes precedence when present.

---

## Security notes

- Passwords are hashed with PBKDF2 (100 000 iterations, 16-byte per-user salt, SHA-256).
- API endpoints use session cookies (`HttpOnly`, `SameSite=Lax`, `Secure` in production).
- Admin endpoints are gated by a `SessionAuth(Role="Admin")` filter — client-side role checks are only for UX.
- The storefront's `/api/cashier/products` endpoint is intentionally public (catalog browsing).
- Checkout runs in a database transaction with row-level `FOR UPDATE` locks to prevent overselling.

---

## Routes

| URL | Audience |
|---|---|
| `/` | Public storefront |
| `/product.html?id=X` | Single product detail page |
| `/login` | Customer & staff sign-in / sign-up |
| `/admin` | Admin dashboard (redirects to admin login if not signed in) |
| `/admin-login.html` | Dedicated admin sign-in page |
| `/cashier` | Cashier terminal |
| `/healthz` | Health check (returns `{"status":"ok"}`) |

---

## License & credits

Author: Ochilov Ilyosjon (B2300540).
