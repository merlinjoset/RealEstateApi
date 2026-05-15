# EstatikImporter

One-off CLI to migrate the **439 properties** from the old
`joseforland.com` WordPress site (running the Estatik plugin) into the
Jose For Land .NET API's PostgreSQL database.

## Two modes

### `--analyze <wxr.xml>` (do this first)

Reads the WP export, prints:

- Counts per `post_type` (so you know how many `es_property` rows are coming)
- The full set of `meta_key`s for the property post type with sample values
- Taxonomies used per post type

**No DB writes.** Send the output back so the mapper field names can be
validated against the real Estatik install (default keys are best-guess).

```pwsh
cd RealEstateApi
dotnet run --project Tools/EstatikImporter -- --analyze C:\path\to\joseforland.WordPress.2026-05-15.xml
```

### `--import <wxr.xml>` (after analyze + mapper review)

Same input, but persists. Idempotent on `Property.WpPostId`:

- WP post not yet in DB → INSERT
- WP post already imported → UPDATE the row in place (preserves our internal Id and any linked inquiries)
- Drafts / trashed posts → skipped
- Rows missing price or area → skipped with a warning

```pwsh
cd RealEstateApi
dotnet run --project Tools/EstatikImporter -- --import C:\path\to\joseforland.WordPress.2026-05-15.xml
```

Confirms with `Type 'yes' to continue:` before writing.

## How it picks the DB

`appsettings.json` / `appsettings.Development.json` in the API project
provide `ConnectionStrings:DefaultConnection`. To point at a different
DB (e.g. a clone for dry runs), override:

```pwsh
$env:ConnectionStrings__DefaultConnection = "Host=...;Database=...;Username=...;Password=..."
dotnet run --project Tools/EstatikImporter -- --import wp-export.xml
```

## What's preserved

- `Property.WpPostId = wp_posts.ID` — lets us serve a 301 from
  `/property/{wpPostId}` → `/properties/{ourId}` after DNS cutover.
- Title, description (HTML stripped), price, area, lat/lng, address bits,
  features, image URLs, road-access flag, legal status, created date.

## What's NOT preserved (yet)

- **Images** — URLs only. The actual `wp-content/uploads/...` files need to
  be either kept on a legacy subdomain or downloaded + rehosted. The
  importer reads the URLs from Estatik meta; URLs that 404 after cutover
  will need a separate sweep.
- **Users** — passwords (WP's phpass hashes aren't BCrypt-compatible).
  Forced password reset via `/forgot-password` on first login.
- **Comments / inquiries** — depends on whether the WP site uses comments
  for buyer inquiries. Tell me what's there and we can extend the importer.

## After import

1. `SELECT COUNT(*) FROM "Properties" WHERE "WpPostId" IS NOT NULL` — should
   match the analyze count.
2. Add nginx redirect rule on the frontend container:
   ```
   location ~ ^/property/(\d+)/?$ { return 301 https://joseforland.com/properties/$1; }
   ```
   (the IDs we mapped match — Estatik's numeric `/property/679/` becomes
   our `/properties/679` because we kept `WpPostId` and our DB inserts
   start fresh.)
3. DNS cutover `joseforland.com` → Render frontend.
