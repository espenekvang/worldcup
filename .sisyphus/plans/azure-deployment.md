# Azure Container Apps Deployment

## TL;DR

> **Quick Summary**: Deploy the World Cup tipping app to Azure Container Apps using a single Docker container (serving both .NET API and React frontend), with Terraform for infrastructure, GitHub Actions for CI/CD, and SQLite on local disk with backup to Azure File Share.
> 
> **Deliverables**:
> - Backend changes (static file serving, matches.json path fix, auto-migration, SQLite backup/restore)
> - Dockerfile (multi-stage build)
> - .dockerignore
> - Terraform infrastructure files
> - GitHub Actions deployment workflow
> - DEPLOYMENT.md with setup instructions
> 
> **Estimated Effort**: Medium
> **Parallel Execution**: YES - 3 waves
> **Critical Path**: Task 1 → Task 2 → Task 3 (local Docker verification) → Task 4-6 (infra + CI + docs)

---

## Context

### Original Request
Deploy the FIFA World Cup 2026 tipping app to Azure so friends can use it during the tournament. Keep it simple and cheap.

### Interview Summary
**Key Discussions**:
- **Platform**: Azure Container Apps (Docker) — user chose this over App Service and VM
- **CI/CD**: GitHub Actions — auto-deploy on push to main
- **Persistence**: SQLite with persistent storage (Azure File Share)
- **IaC**: Terraform (user preference over Bicep)
- **Region**: North Europe (Ireland)
- **Domain**: Azure default URL is fine, no custom domain needed
- **Secrets**: Google OAuth, JWT key, admin email managed as Container Apps secrets + GitHub secrets
- **Budget**: Minimal — friend group use case

### Metis Review
**Identified Gaps** (addressed):
- **SQLite on Azure File Share is broken**: SMB doesn't support POSIX byte-range locking required by SQLite WAL mode. Resolved: use SQLite on local container disk + backup/restore strategy via Azure File Share
- **matches.json path hardcoded**: `../../src/data/matches.json` breaks in Docker container. Resolved: copy into .NET project and use content-root-relative path
- **No static file serving in Program.cs**: Must add `UseStaticFiles()` + SPA fallback for single-container approach
- **VITE_API_URL defaults to localhost**: Must set to `""` (empty) at build time for same-origin deployment
- **CORS hardcodes localhost**: Fine for single-container (same-origin), no changes needed
- **Google OAuth redirect URI**: Must update Google Cloud Console post-deployment (manual step, documented)

---

## Work Objectives

### Core Objective
Deploy the existing World Cup tipping app to Azure Container Apps so it's accessible to a friend group via a public URL.

### Concrete Deliverables
- Modified `api/WorldCup.Api/Program.cs` — static file serving, SPA fallback, auto-migration, configurable matches.json path
- SQLite backup/restore service in `api/WorldCup.Api/Services/SqliteBackupService.cs`
- `Dockerfile` at repo root — multi-stage build (Node → .NET SDK → Runtime)
- `.dockerignore` at repo root
- `terraform/main.tf`, `terraform/acr.tf`, `terraform/container-apps.tf`, `terraform/storage.tf`, `terraform/variables.tf`, `terraform/outputs.tf`
- `terraform/terraform.tfvars.example` — documented variable template
- `.github/workflows/deploy.yml` — CI/CD pipeline
- `DEPLOYMENT.md` — step-by-step first-time setup guide

### Definition of Done
- [ ] `docker build` succeeds and container starts serving both API and frontend
- [ ] `terraform validate` and `terraform plan` succeed
- [ ] GitHub Actions workflow is valid YAML with correct trigger and steps
- [ ] Pushing to main triggers build → push → deploy pipeline

### Must Have
- Single Docker container serving both .NET API and React SPA
- Terraform-managed infrastructure (ACR, Container Apps, File Share)
- GitHub Actions CI/CD deploying on push to main
- SQLite database survives container restarts via backup/restore
- All secrets managed via Container Apps secrets + GitHub secrets
- Auto-migration on startup

### Must NOT Have (Guardrails)
- No custom domain or SSL certificate setup
- No monitoring, Application Insights, or alerting
- No staging/production environment separation
- No Docker Compose (single container)
- No Helm charts or Kubernetes manifests
- No health check endpoints
- No Swagger in production
- No structured logging or Serilog
- No rate limiting or security headers middleware
- No separate nginx/caddy reverse proxy
- No Terraform modules (flat structure, one-off deployment)
- No changes to business logic, models, DTOs, or controllers
- No branch protection or PR-based deployment workflows
- No tests for infrastructure code

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** — ALL verification is agent-executed. No exceptions.

### Test Decision
- **Infrastructure exists**: YES (vitest)
- **Automated tests**: Run existing tests to verify no regressions. No new tests for infra.
- **Framework**: vitest (existing) + Docker build verification

### QA Policy
Every task MUST include agent-executed QA scenarios.
Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **Backend changes**: Bash (dotnet build, docker run, curl)
- **Docker**: Bash (docker build, docker run, curl endpoints)
- **Terraform**: Bash (terraform init, validate, plan)
- **GitHub Actions**: Bash (YAML validation, content checks)

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start Immediately — backend changes + Docker setup):
├── Task 1: Backend changes (Program.cs, SQLite backup service, matches.json path) [deep]
├── Task 2: Dockerfile + .dockerignore [unspecified-high]

Wave 2 (After Wave 1 — verify Docker locally, then infra + CI in parallel):
├── Task 3: Local Docker verification (build + run + test) [quick]

Wave 3 (After Task 3 — infrastructure + CI + docs, MAX PARALLEL):
├── Task 4: Terraform infrastructure files [deep]
├── Task 5: GitHub Actions workflow [unspecified-high]
├── Task 6: DEPLOYMENT.md setup guide [writing]

Wave FINAL (After ALL tasks — verification):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Code quality review (unspecified-high)
├── Task F3: Real manual QA (unspecified-high)
├── Task F4: Scope fidelity check (deep)
-> Present results -> Get explicit user okay

Critical Path: Task 1 → Task 2 → Task 3 → Task 4 → F1-F4 → user okay
Parallel Speedup: ~40% faster than sequential (Wave 3 runs 3 tasks in parallel)
Max Concurrent: 3 (Wave 3)
```

### Dependency Matrix

| Task | Depends On | Blocks | Wave |
|------|-----------|--------|------|
| 1 | — | 2, 3 | 1 |
| 2 | 1 | 3 | 1 |
| 3 | 1, 2 | 4, 5, 6 | 2 |
| 4 | 3 | F1-F4 | 3 |
| 5 | 3 | F1-F4 | 3 |
| 6 | 3 | F1-F4 | 3 |

### Agent Dispatch Summary

- **Wave 1**: **2** — T1 → `deep`, T2 → `unspecified-high`
- **Wave 2**: **1** — T3 → `quick`
- **Wave 3**: **3** — T4 → `deep`, T5 → `unspecified-high`, T6 → `writing`
- **FINAL**: **4** — F1 → `oracle`, F2 → `unspecified-high`, F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

- [x] 1. Backend Changes for Production Deployment

  **What to do**:
  - **Add static file serving to Program.cs**: Insert `app.UseStaticFiles()` before `app.MapControllers()` and `app.MapFallbackToFile("index.html")` after `app.MapControllers()` to serve the React SPA from wwwroot
  - **Fix matches.json loading path**: The current path `Path.Combine(builder.Environment.ContentRootPath, "..", "..", "src", "data", "matches.json")` breaks in Docker. Change to load from a configurable path with fallback: first check environment variable `MATCHES_JSON_PATH`, then fall back to `Path.Combine(builder.Environment.ContentRootPath, "data", "matches.json")`, then fall back to current relative path (for dev compatibility)
  - **Add auto-migration on startup**: **IMPORTANT: SQLite backup restore MUST run BEFORE migration.** Do NOT use a BackgroundService for restore — instead, add synchronous restore logic directly in Program.cs before the migration call. The flow must be: (1) Build app, (2) Check if local DB exists, if not and backup exists at configured path, copy backup to local path, (3) THEN run `Database.Migrate()`. The migration creates/updates the schema but must operate on the restored backup if one exists. After `var app = builder.Build();`, add: restore-from-backup logic, THEN scope-based migration: `using (var scope = app.Services.CreateScope()) { scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate(); }`
  - **Add SQLite backup service**: Create `api/WorldCup.Api/Services/SqliteBackupService.cs` as a `BackgroundService` for **periodic backup ONLY** (not restore — restore is handled synchronously at startup as described above). Every 5 minutes, copy local DB to backup location (use File.Copy with overwrite). Make backup path configurable via `Backup:Path` config (default: `/mnt/backup`). Only activate if backup path exists (skip in development). Register in Program.cs with `builder.Services.AddHostedService<SqliteBackupService>()`
  - **Make CORS origins configurable**: Add a `Cors:Origins` configuration array. In development, default to localhost:5173/5174. In production, read from config. Keep existing CORS policy name.

  **Must NOT do**:
  - Do NOT change any controllers, models, DTOs, or business logic
  - Do NOT remove the development CORS policy or OpenAPI endpoint
  - Do NOT add health check endpoints
  - Do NOT add structured logging or Serilog
  - Do NOT change JWT auth configuration structure

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: Multiple interconnected backend changes requiring understanding of .NET middleware pipeline ordering and EF Core lifecycle
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 2 partially — Task 2 depends on knowing the final Program.cs structure)
  - **Parallel Group**: Wave 1
  - **Blocks**: Task 2, Task 3
  - **Blocked By**: None (can start immediately)

  **References**:

  **Pattern References**:
  - `api/WorldCup.Api/Program.cs` — The entire file. Currently has no static file serving middleware. Lines 54-67 show the middleware pipeline where UseStaticFiles and MapFallbackToFile must be inserted. Line 13 shows the hardcoded matches.json path that needs to be fixed.
  - `api/WorldCup.Api/Services/MatchSchedule.cs` — Lines 25-31 show `LoadFromJson` which reads the file path. No changes needed here, just understand how it's called.
  - `api/WorldCup.Api/Data/AppDbContext.cs` — The EF Core context needed for auto-migration. Find it and understand the constructor.

  **API/Type References**:
  - `api/WorldCup.Api/WorldCup.Api.csproj` — Check existing package references. Ensure Microsoft.AspNetCore.StaticFiles is available (it's included in the ASP.NET Core framework, no extra NuGet package needed).

  **External References**:
  - ASP.NET Core static files middleware: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files
  - ASP.NET Core SPA fallback: `MapFallbackToFile("index.html")` serves index.html for all non-API routes
  - BackgroundService pattern: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Backend builds without errors after changes
    Tool: Bash
    Preconditions: .NET 10 SDK installed
    Steps:
      1. Run `dotnet build` in api/WorldCup.Api/
      2. Check exit code is 0
      3. Check output contains "Build succeeded"
    Expected Result: Build succeeds with 0 errors, 0 warnings
    Failure Indicators: Non-zero exit code, "error" in output
    Evidence: .sisyphus/evidence/task-1-dotnet-build.txt

  Scenario: Existing npm tests still pass (no frontend regression)
    Tool: Bash
    Preconditions: node_modules installed
    Steps:
      1. Run `npm test` from project root
      2. Check all 8 tests pass
    Expected Result: "8 passed (8)" in output
    Failure Indicators: Any test failures or non-zero exit code
    Evidence: .sisyphus/evidence/task-1-npm-test.txt

  Scenario: matches.json path fallback works for development
    Tool: Bash
    Preconditions: dotnet build succeeded
    Steps:
      1. Run `dotnet run` from api/WorldCup.Api/ (with dev config)
      2. Check logs for no FileNotFoundException
      3. Verify app starts (look for "Now listening on")
      4. Stop the process
    Expected Result: App starts successfully, loads matches.json from relative path
    Failure Indicators: FileNotFoundException, crash on startup
    Evidence: .sisyphus/evidence/task-1-dev-startup.txt
  ```

  **Commit**: YES
  - Message: `feat(api): add static file serving, SPA fallback, auto-migration, and SQLite backup`
  - Files: `api/WorldCup.Api/Program.cs`, `api/WorldCup.Api/Services/SqliteBackupService.cs`
  - Pre-commit: `dotnet build` in api/WorldCup.Api/

- [x] 2. Dockerfile and .dockerignore

  **What to do**:
  - **Create `.dockerignore`** at repo root: exclude `node_modules/`, `dist/`, `.git/`, `*.db`, `*.db-shm`, `*.db-wal`, `api/**/bin/`, `api/**/obj/`, `.env`, `appsettings.Development.json`, `.sisyphus/`, `terraform/`
  - **Create `Dockerfile`** at repo root with multi-stage build:
    - **Stage 1 (frontend-build)**: From `node:20-alpine`. Copy `package.json`, `package-lock.json`, run `npm ci`. Copy `src/`, `index.html`, `vite.config.ts`, `tsconfig*.json`. ARG `VITE_GOOGLE_CLIENT_ID`. Set env `VITE_API_URL=""` (empty — same-origin in production). Run `npm run build` — produces `dist/`.
    - **Stage 2 (backend-build)**: From `mcr.microsoft.com/dotnet/sdk:10.0`. Copy `api/WorldCup.Api/`. Run `dotnet publish -c Release -o /publish`. Copy React `dist/` output from Stage 1 into `/publish/wwwroot/`. Copy `src/data/matches.json` into `/publish/data/matches.json` (so the runtime can find it).
    - **Stage 3 (runtime)**: From `mcr.microsoft.com/dotnet/aspnet:10.0`. Copy `/publish` from Stage 2 to `/app`. Set `WORKDIR /app`. Set `ENV ASPNETCORE_ENVIRONMENT=Production`. Set `ENV ASPNETCORE_URLS=http://+:8080`. `EXPOSE 8080`. Create `/app/data` directory for SQLite. `ENTRYPOINT ["dotnet", "WorldCup.Api.dll"]`.
  - **IMPORTANT**: The Dockerfile must set `MATCHES_JSON_PATH=/app/data/matches.json` as ENV or ensure the default path in Program.cs resolves correctly to `/app/data/matches.json` in the container

  **Must NOT do**:
  - Do NOT use chiseled/distroless images (keep it debuggable for a friend-group app)
  - Do NOT add Docker Compose
  - Do NOT include SDK tools in the final runtime image
  - Do NOT copy node_modules or .NET build artifacts into final image
  - Do NOT hardcode secrets in the Dockerfile
  - Do NOT copy appsettings.Development.json into the image

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Docker multi-stage build requires careful layer ordering and understanding of both Node and .NET build outputs
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO (depends on Task 1 for final Program.cs structure)
  - **Parallel Group**: Wave 1 (sequential after Task 1)
  - **Blocks**: Task 3
  - **Blocked By**: Task 1

  **References**:

  **Pattern References**:
  - `package.json:9` — Build command: `"build": "tsc -b && vite build"` produces `dist/`
  - `vite.config.ts` — Check for any custom outDir or publicDir settings
  - `api/WorldCup.Api/WorldCup.Api.csproj` — Needed for dotnet publish. Check TargetFramework and any special build properties.
  - `src/api/client.ts:1` — `const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5211'` — VITE_API_URL must be `""` at build time
  - `src/main.tsx` — Uses `import.meta.env.VITE_GOOGLE_CLIENT_ID` — must be passed as build arg

  **External References**:
  - .NET Docker multi-stage builds: https://learn.microsoft.com/en-us/dotnet/core/docker/build-container
  - Node alpine for frontend builds: standard practice for minimal image size

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Docker image builds successfully
    Tool: Bash
    Preconditions: Docker installed and running
    Steps:
      1. Run `docker build -t worldcup-test --build-arg VITE_GOOGLE_CLIENT_ID=test .` from repo root
      2. Check exit code is 0
      3. Verify image exists: `docker images worldcup-test`
    Expected Result: Build completes, image listed with tag "latest"
    Failure Indicators: Non-zero exit code, build errors
    Evidence: .sisyphus/evidence/task-2-docker-build.txt

  Scenario: Docker image is reasonably sized (no SDK bloat)
    Tool: Bash
    Preconditions: Image built from previous scenario
    Steps:
      1. Run `docker images worldcup-test --format "{{.Size}}"`
      2. Parse the size value
    Expected Result: Image size < 500MB (runtime + app, no SDK)
    Failure Indicators: Image > 1GB suggests SDK or node_modules leaked into final stage
    Evidence: .sisyphus/evidence/task-2-image-size.txt

  Scenario: .dockerignore prevents large files from being sent to daemon
    Tool: Bash
    Preconditions: .dockerignore exists
    Steps:
      1. Read .dockerignore and verify node_modules/, .git/, *.db patterns exist
    Expected Result: All expected exclusions present
    Failure Indicators: Missing critical exclusions
    Evidence: .sisyphus/evidence/task-2-dockerignore.txt
  ```

  **Commit**: YES
  - Message: `feat: add Dockerfile and .dockerignore for containerized deployment`
  - Files: `Dockerfile`, `.dockerignore`
  - Pre-commit: `docker build -t worldcup-test --build-arg VITE_GOOGLE_CLIENT_ID=test .`

---

- [x] 3. Local Docker Verification

  **What to do**:
  - Build the Docker image: `docker build -t worldcup-test --build-arg VITE_GOOGLE_CLIENT_ID=test .`
  - Run the container with all required env vars:
    ```
    docker run -d -p 8080:8080 \
      -e Jwt__Key=TestKeyThatIsAtLeast32CharactersLong \
      -e Jwt__Issuer=test \
      -e Jwt__Audience=test \
      -e Google__ClientId=test \
      -e Admin__Email=test@test.com \
      -e "ConnectionStrings__DefaultConnection=Data Source=/app/data/worldcup.db" \
      --name wc-test worldcup-test
    ```
  - Verify the container serves both frontend and API
  - Check logs for no errors (especially no FileNotFoundException for matches.json)
  - Clean up: `docker stop wc-test && docker rm wc-test`
  - **If any issues found**: fix them in the relevant files (Dockerfile, Program.cs) and re-test

  **Must NOT do**:
  - Do NOT push the image anywhere — this is local verification only
  - Do NOT modify Terraform or GitHub Actions in this task

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Verification task — run commands and check output, minimal code changes (only bug fixes if needed)
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO (gate task — must pass before Wave 3)
  - **Parallel Group**: Wave 2 (solo)
  - **Blocks**: Task 4, Task 5, Task 6
  - **Blocked By**: Task 1, Task 2

  **References**:

  **Pattern References**:
  - Task 1 and Task 2 outputs — the Dockerfile and modified Program.cs
  - `api/WorldCup.Api/appsettings.json` — Check default configuration values for reference

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Container starts and serves React SPA at root
    Tool: Bash
    Preconditions: Docker image built, container running on port 8080
    Steps:
      1. Wait 5 seconds for container startup
      2. Run `curl -s http://localhost:8080/`
      3. Check response contains `<div id="root">`
    Expected Result: HTTP 200, HTML response with React mount point
    Failure Indicators: Connection refused, 404, no "root" div
    Evidence: .sisyphus/evidence/task-3-spa-serve.txt

  Scenario: API endpoints are reachable
    Tool: Bash
    Preconditions: Container running
    Steps:
      1. Run `curl -s -o /dev/null -w "%{http_code}" -X POST -H "Content-Type: application/json" -d '{}' http://localhost:8080/api/auth/google`
      2. Check response code is 400 (endpoint exists, rejects invalid body)
    Expected Result: HTTP 400 (NOT 404 or 405)
    Failure Indicators: 404 (routing broken), 405 (method not allowed), connection refused
    Evidence: .sisyphus/evidence/task-3-api-reachable.txt

  Scenario: Container logs show no errors
    Tool: Bash
    Preconditions: Container running for at least 5 seconds
    Steps:
      1. Run `docker logs wc-test`
      2. Check output does NOT contain "FileNotFoundException", "Failed to parse", "Unhandled exception"
      3. Check output contains "Now listening on" or equivalent startup message
    Expected Result: Clean startup with no exceptions
    Failure Indicators: Any exception or "Failed" message in logs
    Evidence: .sisyphus/evidence/task-3-container-logs.txt

  Scenario: Static assets (JS/CSS) are served
    Tool: Bash
    Preconditions: Container running
    Steps:
      1. Run `curl -s http://localhost:8080/` and extract the JS bundle path from the HTML (e.g., `/assets/index-*.js`)
      2. Run `curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/assets/index-<hash>.js`
    Expected Result: HTTP 200 for the JS bundle
    Failure Indicators: 404 for static assets
    Evidence: .sisyphus/evidence/task-3-static-assets.txt
  ```

  **Commit**: NO (verification only, unless bug fixes are needed)

---

- [x] 4. Terraform Infrastructure Files

  **What to do**:
  - Create `terraform/` directory at repo root with these files:
  - **`terraform/main.tf`**: Configure `azurerm` provider `~> 4.0`, create Resource Group `rg-worldcup` in `northeurope`, create Log Analytics Workspace (SKU `PerGB2018`, 30 day retention — note: `Free` SKU is removed in azurerm 4.x)
  - **`terraform/acr.tf`**: Create Azure Container Registry, Basic SKU (~$5/mo). Name must be globally unique — use a variable with a default like `acrworldcup<random>`. Enable admin (needed for initial push before managed identity is set up).
  - **`terraform/storage.tf`**: Create Storage Account (Standard_LRS, minimum tier) + File Share named `worldcup-backup` for SQLite backup volume
  - **`terraform/container-apps.tf`**: Create Container Apps Environment linked to Log Analytics. Create Container App with:
    - `revision_mode = "Single"`
    - Container: **Use a placeholder image for initial creation** (`mcr.microsoft.com/k8se/quickstart:latest`) since the ACR image doesn't exist yet at `terraform apply` time. The placeholder listens on port 80, so `target_port` must be `80` initially. The first deployment (manual Step 4 or GitHub Actions) will replace the image via `az containerapp update` and switch the port to 8080 via `az containerapp ingress update`. This avoids the bootstrap chicken-and-egg problem.
    - 0.25 CPU / 0.5Gi memory (minimum), initial port 80 (placeholder), real app port 8080
    - Ingress: external, target_port 80 (placeholder), `allow_insecure_traffic = false`
    - Secrets: `jwt-key`, `google-client-id`, `admin-email` (sourced from variables)
    - Environment variables mapping secrets using .NET `__` convention: `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Google__ClientId`, `Admin__Email`, `ConnectionStrings__DefaultConnection`
    - Volume mount: Azure File Share at `/mnt/backup`
    - `min_replicas = 0`, `max_replicas = 1` (scale to zero when idle, max 1 for SQLite)
    - System-assigned managed identity with AcrPull role on the ACR
    - Use `lifecycle { ignore_changes = [template[0].container[0].image, ingress[0].target_port] }` to prevent Terraform from reverting the image tag or target port after GitHub Actions deploys a new version (placeholder uses port 80, real app uses port 8080)
  - **`terraform/variables.tf`**: Define all input variables with descriptions: `resource_group_name`, `location` (default: "northeurope"), `acr_name`, `container_app_name`, `jwt_key` (sensitive), `jwt_issuer`, `jwt_audience`, `google_client_id` (sensitive), `admin_email`, `vite_google_client_id`
  - **`terraform/outputs.tf`**: Output: Container App URL (FQDN), ACR login server, Resource Group name
  - **`terraform/terraform.tfvars.example`**: Template with all variables and comments explaining each one. Mark sensitive values with `# SENSITIVE - do not commit actual values`

  **Must NOT do**:
  - Do NOT create Key Vault (overkill for this use case)
  - Do NOT create Application Insights or monitoring
  - Do NOT create custom domain or certificate resources
  - Do NOT create multiple environments
  - Do NOT abstract into Terraform modules
  - Do NOT run `terraform apply` — this task only creates the files
  - Do NOT include actual secret values in any file

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: Complex Terraform configuration with Container Apps, ACR, managed identity, volume mounts, and secret management requires deep Azure knowledge
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 5 and Task 6)
  - **Parallel Group**: Wave 3
  - **Blocks**: F1-F4
  - **Blocked By**: Task 3

  **References**:

  **Pattern References**:
  - `api/WorldCup.Api/Program.cs:29-34` — JWT configuration keys: `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` — maps to `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` in Container Apps env vars
  - `api/WorldCup.Api/Program.cs:16-17` — Connection string key: `ConnectionStrings:DefaultConnection`
  - `api/WorldCup.Api/Controllers/AuthController.cs` — Google:ClientId and Admin:Email config keys (verify exact key names)

  **External References**:
  - Terraform azurerm Container Apps: https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/container_app
  - Terraform azurerm Container Registry: https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/container_registry
  - Azure Container Apps volume mounts: https://learn.microsoft.com/en-us/azure/container-apps/storage-mounts

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Terraform configuration is valid
    Tool: Bash
    Preconditions: Terraform CLI installed
    Steps:
      1. Run `terraform -chdir=terraform init -backend=false`
      2. Check exit code is 0
      3. Run `terraform -chdir=terraform validate`
      4. Check exit code is 0 and output says "Success"
    Expected Result: Both commands succeed
    Failure Indicators: Non-zero exit code, validation errors
    Evidence: .sisyphus/evidence/task-4-terraform-validate.txt

  Scenario: All required Azure resources are defined
    Tool: Bash
    Preconditions: Terraform files exist in terraform/
    Steps:
      1. Grep terraform/*.tf for each required resource type:
         - azurerm_resource_group
         - azurerm_container_registry
         - azurerm_log_analytics_workspace
         - azurerm_container_app_environment
         - azurerm_container_app
         - azurerm_storage_account
         - azurerm_storage_share
      2. Verify each is present
    Expected Result: All 7 resource types found
    Failure Indicators: Any resource type missing
    Evidence: .sisyphus/evidence/task-4-resources-check.txt

  Scenario: Sensitive variables are marked sensitive
    Tool: Bash
    Preconditions: terraform/variables.tf exists
    Steps:
      1. Check that jwt_key variable has `sensitive = true`
      2. Check that google_client_id variable has `sensitive = true`
    Expected Result: Both sensitive variables properly marked
    Failure Indicators: Missing sensitive flag on secrets
    Evidence: .sisyphus/evidence/task-4-sensitive-vars.txt
  ```

  **Commit**: YES
  - Message: `feat(infra): add Terraform configuration for Azure Container Apps`
  - Files: `terraform/main.tf`, `terraform/acr.tf`, `terraform/container-apps.tf`, `terraform/storage.tf`, `terraform/variables.tf`, `terraform/outputs.tf`, `terraform/terraform.tfvars.example`
  - Pre-commit: `terraform -chdir=terraform validate`

- [x] 5. GitHub Actions Deployment Workflow

  **What to do**:
  - Create `.github/workflows/deploy.yml` with:
    - **Trigger**: `on: push: branches: [main]`; also `on: workflow_dispatch:` for manual runs
    - **Job: build-and-deploy** running on `ubuntu-latest`
    - **Steps**:
      1. `actions/checkout@v4`
      2. `azure/login@v2` — Login to Azure using OIDC (preferred) or service principal stored in GitHub secrets (`AZURE_CREDENTIALS` or `AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID`)
      3. `az acr build` — Build and push image directly on ACR (avoids needing Docker locally on the runner): `az acr build --registry ${{ secrets.ACR_NAME }} --image worldcup:${{ github.sha }} --build-arg VITE_GOOGLE_CLIENT_ID=${{ secrets.VITE_GOOGLE_CLIENT_ID }} .`
      4. `az containerapp update` + `az containerapp ingress update` — Deploy new image and switch to correct port: first `az containerapp update --name ${{ secrets.CONTAINER_APP_NAME }} --resource-group ${{ secrets.RESOURCE_GROUP }} --image ${{ secrets.ACR_NAME }}.azurecr.io/worldcup:${{ github.sha }}`, then `az containerapp ingress update --name ${{ secrets.CONTAINER_APP_NAME }} --resource-group ${{ secrets.RESOURCE_GROUP }} --target-port 8080`
  - **Required GitHub repository secrets** (document in comments in the YAML):
    - `AZURE_CREDENTIALS` (or OIDC: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`)
    - `ACR_NAME` — ACR resource name
    - `RESOURCE_GROUP` — Azure resource group name
    - `CONTAINER_APP_NAME` — Container App name
    - `VITE_GOOGLE_CLIENT_ID` — Google OAuth client ID (baked into frontend build)

  **Must NOT do**:
  - Do NOT run `terraform apply` in the pipeline
  - Do NOT add branch protection rules
  - Do NOT add PR-based deployment workflows
  - Do NOT add test steps (keep pipeline simple — tests run locally)
  - Do NOT add Docker layer caching (unnecessary complexity for this use case)

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: GitHub Actions YAML with Azure CLI commands requires knowledge of both platforms
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 4 and Task 6)
  - **Parallel Group**: Wave 3
  - **Blocks**: F1-F4
  - **Blocked By**: Task 3

  **References**:

  **Pattern References**:
  - `Dockerfile` (from Task 2) — The build arg `VITE_GOOGLE_CLIENT_ID` must match what the workflow passes

  **External References**:
  - azure/login action: https://github.com/azure/login
  - az acr build: https://learn.microsoft.com/en-us/cli/azure/acr?view=azure-cli-latest#az-acr-build
  - az containerapp update: https://learn.microsoft.com/en-us/cli/azure/containerapp?view=azure-cli-latest#az-containerapp-update

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Workflow YAML is valid
    Tool: Bash
    Preconditions: .github/workflows/deploy.yml exists, Node.js installed (project dependency)
    Steps:
      1. Run `node -e "const fs=require('fs'); const f=fs.readFileSync('.github/workflows/deploy.yml','utf8'); if(!f.includes('on:')) throw 'missing trigger'; if(!f.includes('jobs:')) throw 'missing jobs'; console.log('Basic structure valid')"`
      2. Check exit code is 0
    Expected Result: Script runs without error, prints "Basic structure valid"
    Failure Indicators: Non-zero exit code, missing required YAML keys
    Evidence: .sisyphus/evidence/task-5-yaml-valid.txt

  Scenario: Workflow has correct trigger and required steps
    Tool: Bash
    Preconditions: Workflow file exists
    Steps:
      1. Grep for `push:` and `branches:` containing `main`
      2. Grep for `azure/login`
       3. Grep for `az acr build`
       4. Grep for `az containerapp update`
       5. Grep for `VITE_GOOGLE_CLIENT_ID`
       6. Grep for `az containerapp ingress update`
       7. Grep for `--target-port 8080`
     Expected Result: All 7 patterns found
    Failure Indicators: Any pattern missing
    Evidence: .sisyphus/evidence/task-5-workflow-content.txt
  ```

  **Commit**: YES
  - Message: `feat(ci): add GitHub Actions deployment workflow`
  - Files: `.github/workflows/deploy.yml`
  - Pre-commit: `node -e "const fs=require('fs'); const f=fs.readFileSync('.github/workflows/deploy.yml','utf8'); if(!f.includes('on:') || !f.includes('jobs:')) throw 'invalid'; console.log('ok')"`

- [x] 6. Deployment Documentation (DEPLOYMENT.md)

  **What to do**:
  - Create `DEPLOYMENT.md` at repo root with concise, step-by-step instructions:
    - **Prerequisites**: Azure CLI, Terraform CLI, Docker, GitHub repo access, Google Cloud Console access
    - **Step 1: Azure Setup**: `az login`, create service principal for GitHub Actions (or set up OIDC), note credentials
    - **Step 2: Configure Variables**: Copy `terraform/terraform.tfvars.example` to `terraform/terraform.tfvars`, fill in values (JWT key ≥32 chars, Google Client ID, admin email)
    - **Step 3: Deploy Infrastructure**: `cd terraform && terraform init && terraform plan -var-file=terraform.tfvars && terraform apply -var-file=terraform.tfvars`. Note the outputs (ACR name, Container App URL, Resource Group). The Container App is created with a placeholder image — this is expected.
    - **Step 4: Build and Deploy App Image**: Push the real app image: `az acr build --registry <ACR_NAME> --image worldcup:initial --build-arg VITE_GOOGLE_CLIENT_ID=<your-google-client-id> .` then update the image: `az containerapp update --name <APP_NAME> --resource-group <RG_NAME> --image <ACR_NAME>.azurecr.io/worldcup:initial` then switch the port from placeholder (80) to real app (8080): `az containerapp ingress update --name <APP_NAME> --resource-group <RG_NAME> --target-port 8080`.
    - **Step 5: Configure GitHub Secrets**: List all required repository secrets and where to get each value
    - **Step 6: Update Google OAuth**: Add the Container App URL to Google Cloud Console → OAuth consent screen → Authorized JavaScript origins
    - **Step 7: Verify**: curl the Container App URL, verify login page loads
    - **Ongoing**: Push to main → auto-deploys via GitHub Actions
    - **Troubleshooting**: Common issues (container not starting, 502 errors, Google OAuth mismatch)

  **Must NOT do**:
  - Do NOT write more than ~150 lines — keep it concise and actionable
  - Do NOT include architecture diagrams or design rationale
  - Do NOT document development setup (already in README.md)

  **Recommended Agent Profile**:
  - **Category**: `writing`
    - Reason: Technical documentation writing task
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 4 and Task 5)
  - **Parallel Group**: Wave 3
  - **Blocks**: F1-F4
  - **Blocked By**: Task 3

  **References**:

  **Pattern References**:
  - `README.md` — Existing project documentation style and structure. Match tone and formatting.
  - `terraform/terraform.tfvars.example` (from Task 4) — Variable names and descriptions
  - `.github/workflows/deploy.yml` (from Task 5) — Required GitHub secrets names
  - `terraform/outputs.tf` (from Task 4) — Output names referenced in steps

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: DEPLOYMENT.md exists and covers all required sections
    Tool: Bash
    Preconditions: File created
    Steps:
      1. Read DEPLOYMENT.md
      2. Check it contains sections: Prerequisites, Azure Setup, Configure Variables, Deploy Infrastructure, GitHub Secrets, Google OAuth, Verify, Troubleshooting
    Expected Result: All sections present
    Failure Indicators: Missing sections
    Evidence: .sisyphus/evidence/task-6-doc-sections.txt

  Scenario: All secret/variable names in docs match actual Terraform and GitHub Actions
    Tool: Bash
    Preconditions: DEPLOYMENT.md, terraform/variables.tf, .github/workflows/deploy.yml exist
    Steps:
      1. Extract variable names from DEPLOYMENT.md
      2. Cross-reference with terraform/variables.tf and workflow secrets
    Expected Result: No mismatched or missing variable names
    Failure Indicators: Variable name in docs not found in actual config
    Evidence: .sisyphus/evidence/task-6-var-consistency.txt
  ```

  **Commit**: YES
  - Message: `docs: add deployment guide`
  - Files: `DEPLOYMENT.md`

---

## Final Verification Wave

> 4 review agents run in PARALLEL. ALL must APPROVE. Present consolidated results to user and get explicit "okay" before completing.

- [x] F1. **Plan Compliance Audit** — `oracle`
  Read the plan end-to-end. For each "Must Have": verify implementation exists (read file, docker build, curl endpoint, terraform validate). For each "Must NOT Have": search codebase for forbidden patterns — reject with file:line if found. Check evidence files exist in .sisyphus/evidence/. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [x] F2. **Code Quality Review** — `unspecified-high`
  Run `dotnet build` + `npm run build` + `npm test`. Review all changed files for: commented-out code, console.log in prod, unused imports, hardcoded secrets. Check AI slop: excessive comments, over-abstraction, generic names. Verify Dockerfile follows best practices (layer ordering, .dockerignore, minimal runtime image).
  Output: `Build [PASS/FAIL] | Tests [N pass/N fail] | Files [N clean/N issues] | VERDICT`

- [x] F3. **Real Manual QA** — `unspecified-high`
  Build Docker image locally. Run container with test env vars. Verify: `curl /` returns HTML with `<div id="root">`, `curl -X POST -H "Content-Type: application/json" -d '{}' /api/auth/google` returns 400 (API reachable, rejects empty body), `docker logs` has no errors/exceptions. Test that container starts without matches.json FileNotFoundException. Save evidence to `.sisyphus/evidence/final-qa/`.
  Output: `Scenarios [N/N pass] | Integration [N/N] | Edge Cases [N tested] | VERDICT`

- [x] F4. **Scope Fidelity Check** — `deep`
  For each task: read "What to do", read actual diff (git log/diff). Verify 1:1 — everything in spec was built, nothing beyond spec was built. Check "Must NOT do" compliance. Flag unaccounted changes.
  Output: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

- **After Task 1**: `feat(api): add static file serving, SPA fallback, auto-migration, and SQLite backup` — Program.cs, SqliteBackupService.cs
- **After Task 2**: `feat: add Dockerfile and .dockerignore for containerized deployment` — Dockerfile, .dockerignore
- **After Task 3**: No commit (verification only)
- **After Task 4**: `feat(infra): add Terraform configuration for Azure Container Apps` — terraform/*.tf
- **After Task 5**: `feat(ci): add GitHub Actions deployment workflow` — .github/workflows/deploy.yml
- **After Task 6**: `docs: add deployment guide` — DEPLOYMENT.md

---

## Success Criteria

### Verification Commands
```bash
docker build -t worldcup-test --build-arg VITE_GOOGLE_CLIENT_ID=test .  # Expected: successful build
docker run -d -p 8080:8080 \
  -e Jwt__Key=TestKeyThatIsAtLeast32CharactersLong \
  -e Jwt__Issuer=test -e Jwt__Audience=test \
  -e Google__ClientId=test -e Admin__Email=test@test.com \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/worldcup.db" \
  --name wc-test worldcup-test  # Expected: container starts
curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/  # Expected: 200
curl -s http://localhost:8080/ | grep "root"  # Expected: <div id="root">
curl -s -o /dev/null -w "%{http_code}" -X POST -H "Content-Type: application/json" -d '{}' http://localhost:8080/api/auth/google  # Expected: 400
cd terraform && terraform init && terraform validate  # Expected: Success
npm test  # Expected: 8 passed
```

### Final Checklist
- [ ] All "Must Have" present
- [ ] All "Must NOT Have" absent
- [ ] Existing tests still pass
- [ ] Docker image builds and runs correctly
- [ ] Terraform validates successfully
- [ ] GitHub Actions workflow is valid
