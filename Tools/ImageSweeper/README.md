# ImageSweeper

One-off tool that downloads every property image still hosted at
`joseforland.com/wp-content/uploads/...` (the legacy WordPress site) and
rewrites the `Property.Images` URLs in our PostgreSQL DB to point at the
new media path served by `RealEstateApi` itself.

Why? After we cut DNS for `joseforland.com` to point at the React app,
the old WP media URLs 404. Pre-cutover, we sweep everything to our own
storage so cutover is clean and no listing loses its photos.

## Run it

```pwsh
cd RealEstateApi

# 1. Dry run first — downloads files but doesn't touch the DB
dotnet run --project Tools/ImageSweeper -- --dry-run --out uploads

# 2. Real run — same downloads (now cached on disk) + DB rewrite
dotnet run --project Tools/ImageSweeper -- --out uploads
```

Idempotent: a second `--dry-run` after a successful run reports
`Unique URLs pointing at legacy host: 0` and exits cleanly.

## Args

| flag | default | what |
|---|---|---|
| `--out <path>`       | `uploads`                                | Local folder to download into |
| `--new-base <url>`   | `https://api.joseforland.com/media`      | URL prefix that replaces the WP path |
| `--legacy-host <h>`  | `joseforland.com`                        | Source host — only URLs on this host are swept |
| `--dry-run`          | (off)                                    | Skip the DB write step |
| `--parallel <n>`     | `6`                                      | Concurrent downloads |

## What gets deployed to production

Two things need to land on Render:

1. **The files themselves.** Local `uploads/` folder needs to end up at
   the disk mount path the API serves from (`Storage:UploadsPath` env var,
   defaults to `./uploads`).

   Options:
   - **Render disk** (recommended): attach a persistent disk to the API
     service, mount at e.g. `/var/data/uploads`, set
     `Storage__UploadsPath=/var/data/uploads`. Then upload the local
     `uploads/` folder via SSH/SFTP, or use `render-cli` to rsync.
   - **Build-time bundling**: not viable at 41 MB+ across 422 files — bloats
     every deploy and burns Docker layer cache.

2. **The static-files middleware** that maps `/media/*` to that folder.
   Already wired in `Program.cs` — picks up `Storage:UploadsPath` from
   config on boot and logs the active path.

## Production checklist

- [ ] Attach a Render disk (e.g. 1 GB) to the `RealEstateApi` service
- [ ] Set `Storage__UploadsPath` env var to the mount path
- [ ] Upload the local `uploads/` folder to the disk
- [ ] Verify `https://api.joseforland.com/media/2026/05/somefile.jpeg`
      returns the image
- [ ] Confirm a public property page renders its hero image

## Rerunning after migration

If we later import more properties from a fresh WP export and they bring
in more legacy URLs, just rerun the sweep — it skips files already on
disk and skips DB rows already pointing at the new host.
