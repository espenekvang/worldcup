# Decisions — azure-deployment

## [2026-04-11] Key architectural decisions (from user interview + Metis/Momus review)
- Platform: Azure Container Apps (Docker)
- IaC: Terraform (azurerm ~> 4.0)
- CI/CD: GitHub Actions, push to main triggers deploy
- Region: northeurope
- SQLite persistence: local disk + Azure File Share backup (copy strategy, not mount)
- Secrets: Container Apps secrets + GitHub secrets
- No custom domain, no monitoring, no staging env
- YAML validation in CI uses Node.js (not PyYAML — not available)
- az containerapp ingress update --target-port 8080 for port switch (not az containerapp update --target-port)
- Task 1 keeps restore logic in Program.cs and pins the startup restore source to /mnt/backup/worldcup.db before migration, while the hosted backup service uses configurable Backup:Path (default /mnt/backup) for periodic copies only
