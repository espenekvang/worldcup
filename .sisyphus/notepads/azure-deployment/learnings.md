# Learnings — azure-deployment

## [2026-04-11] Session Start
- Plan has 6 implementation tasks + 4 final verification tasks
- No worktree — working directly in /Users/eekvang/dev/aiworkshop/worldcup
- SQLite strategy: local disk + backup/restore to Azure File Share (NOT direct mount — SMB breaks SQLite WAL locking)
- Single container: .NET serves React static files via UseStaticFiles() + MapFallbackToFile()
- VITE_API_URL="" at Docker build time for same-origin requests
- min_replicas=0 (scale to zero, free when idle)
- Placeholder image mcr.microsoft.com/k8se/quickstart:latest listens on port 80 — initial Terraform uses port 80, real app uses 8080 via az containerapp ingress update
- Log Analytics SKU must be PerGB2018 (not Free — removed in azurerm 4.x)
- lifecycle { ignore_changes = [template[0].container[0].image, ingress[0].target_port] } to prevent Terraform drift
- Restore-from-backup runs BEFORE Database.Migrate() in Program.cs (synchronous, not BackgroundService)
- BackgroundService only does periodic backup (every 5 min)
- Task 1 implementation uses MATCHES_JSON_PATH first, then /app/data/matches.json under content root, then the original ../../src/data/matches.json development fallback to avoid startup FileNotFoundException across dev and container environments
- Dev startup currently emits StaticFileMiddleware warnings when api/WorldCup.Api/wwwroot is absent; static file serving and SPA fallback are wired, but frontend assets still need to be published/copied separately for production

## [2026-04-11] Task 2 — Dockerfile + .dockerignore

- 3-stage build: frontend-build (node:20-alpine), backend-build (dotnet/sdk:10.0), runtime (dotnet/aspnet:10.0)
- VITE_GOOGLE_CLIENT_ID passed as ARG only (baked at build time, not in runtime image)
- VITE_API_URL="" set as ENV in frontend-build stage so Vite bakes empty string into bundle (same-origin API calls)
- React dist/ copied from frontend-build into /publish/wwwroot/ in backend-build stage
- src/data/matches.json copied to /publish/data/matches.json in backend-build stage (lands at /app/data/matches.json in runtime)
- Runtime image /app/data/ dir created via RUN mkdir -p /app/data (for SQLite at runtime)
- Final image size: 304MB (well under 500MB target)
- .dockerignore excludes node_modules/, dist/, .git/, *.db, api/**/bin, api/**/obj, .env, appsettings.Development.json, .sisyphus/, terraform/
- no public/ directory exists in this repo — COPY public/ step not needed
Container started on port 8080 and served SPA/API correctly.
Root HTML contained <div id="root"> and JS bundle path /assets/index-ZPdTmTeX.js returned 200.
POST /api/auth/google with {} returned 400.
docker logs wc-test contained no FileNotFoundException, Failed to parse, or Unhandled exception.

## [2026-04-11] Task 4 — Terraform for Azure Container Apps

- Terraform config validated with azurerm ~> 4.0 using separate files for core, ACR, storage, container apps, variables, outputs, and example tfvars.
- Azure File Share mount requires azurerm_container_app_environment_storage between the share and the container app volume.
- Log Analytics SKU PerGB2018 and lifecycle ignore_changes on image + target_port keep initial ACA bootstrap config valid without drift after real image deployment.
