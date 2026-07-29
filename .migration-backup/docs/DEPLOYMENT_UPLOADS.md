# Deployment: Upload Storage

## Persistent Upload Storage on Railway

Uploaded message attachments (JPG, PNG, PDF) are stored on a **Railway Persistent Volume** to survive backend redeployments.

### Volume Configuration

| Setting | Value |
|---------|-------|
| **Mount Path** | `/data/uploads` |
| **Environment Variable** | `UPLOADS_PATH=/data/uploads` |
| **Min Size** | 1 GB (adjust as needed) |

### Setup Steps (Railway Dashboard)

1. Go to the **backend service** in the Railway project.
2. Click **Volumes** → **New Volume**.
3. Set **Mount Path** to `/data/uploads`.
4. Set the volume size (1 GB minimum recommended).
5. In the **Variables** tab, add: `UPLOADS_PATH=/data/uploads`.
6. Redeploy the backend service.

### How It Works

The app resolves the upload directory in this priority order:

1. **`UPLOADS_PATH` env var** — Used when set (Railway persistent volume).
2. **`wwwroot/uploads/`** — Default path, works if directory is writable.
3. **`/tmp/aqlan-uploads/`** — Fallback for read-only environments (ephemeral, lost on redeploy).

The Dockerfile pre-creates both `/app/wwwroot/uploads` and `/data/uploads` with correct ownership (`appuser:appgroup`), so the non-root container user can write to either path.

### File Serving

Uploaded files are served publicly at `/uploads/{fileName}` via ASP.NET `StaticFileOptions` with a `PhysicalFileProvider` pointing to the resolved uploads directory.

### Permissions

- The container runs as `appuser` (UID 1001, non-root).
- Both upload directories are owned by `appuser:appgroup`.
- If the Railway volume mount changes ownership, set `RAILWAY_RUN_UID=0` as a service variable (Railway will run as root to fix permissions) — but this is generally not needed.

### Future Enhancement: Cloud Object Storage

For scalable, production-grade file storage, consider migrating to:

- **Cloudflare R2** (S3-compatible, no egress fees)
- **AWS S3**
- **Azure Blob Storage**

This would decouple file storage from the compute container entirely and provide:
- Unlimited storage
- CDN integration
- No file loss on redeployment
- Better backup and disaster recovery

The `UPLOADS_PATH` abstraction makes this migration straightforward — only the file storage layer needs to change.
