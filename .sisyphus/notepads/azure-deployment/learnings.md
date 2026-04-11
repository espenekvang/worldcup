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
