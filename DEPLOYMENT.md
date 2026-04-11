# Deployment Guide

Follow these steps for a first-time deployment to Azure.

## Prerequisites

- Azure CLI installed (`az --version`)
- Terraform CLI installed (`terraform --version`)
- Docker installed (for local testing)
- GitHub repository with push access
- Google Cloud Console access (for OAuth)

## Step 1: Azure Setup

1. Log in to Azure:
   ```bash
   az login
   ```
2. Create a service principal for GitHub Actions:
   ```bash
   az ad sp create-for-rbac --name worldcup-deploy --role contributor \
     --scopes /subscriptions/<subscription-id> --sdk-auth
   ```
3. Save the JSON output — you'll need it for the `AZURE_CREDENTIALS` GitHub secret.

## Step 2: Configure Variables

1. Initialize terraform variables:
   ```bash
   cp terraform/terraform.tfvars.example terraform/terraform.tfvars
   ```
2. Fill in `terraform/terraform.tfvars`:
   - `acr_name`: Globally unique (e.g., `acrworldcupXXXXX`)
   - `jwt_key`: Random string (min 32 characters)
   - `google_client_id`: From Google Cloud Console
   - `admin_email`: Your Google account email
   - `vite_google_client_id`: Same as `google_client_id`

## Step 3: Deploy Infrastructure

```bash
cd terraform
terraform init
terraform plan -var-file=terraform.tfvars
terraform apply -var-file=terraform.tfvars
```

Note these outputs: `container_app_url`, `acr_login_server`, `resource_group_name`.

## Step 4: Build and Deploy App Image

1. Push the first real image to ACR:
   ```bash
   az acr build --registry <ACR_NAME> --image worldcup:initial \
     --build-arg VITE_GOOGLE_CLIENT_ID=<your-google-client-id> .
   ```
2. Update the container app with the real image:
   ```bash
   az containerapp update --name <CONTAINER_APP_NAME> \
     --resource-group <RESOURCE_GROUP> \
     --image <ACR_NAME>.azurecr.io/worldcup:initial
   ```
3. Switch port from placeholder (80) to app port (8080):
   ```bash
   az containerapp ingress update --name <CONTAINER_APP_NAME> \
     --resource-group <RESOURCE_GROUP> \
     --target-port 8080
   ```

## Step 5: Configure GitHub Secrets

Add these secrets in GitHub → Settings → Secrets and variables → Actions:

| Secret | Value |
|--------|-------|
| `AZURE_CREDENTIALS` | JSON from Step 1 |
| `ACR_NAME` | ACR name from terraform output |
| `RESOURCE_GROUP` | `rg-worldcup` |
| `CONTAINER_APP_NAME` | Container App name (value you set in terraform.tfvars) |
| `VITE_GOOGLE_CLIENT_ID` | Google OAuth client ID |

## Step 6: Update Google OAuth

1. Go to Google Cloud Console → APIs & Services → Credentials.
2. Edit your OAuth 2.0 Client ID.
3. Add the `container_app_url` (from terraform output) to **Authorized JavaScript origins**.

## Step 7: Verify

```bash
curl https://<container-app-url>/
```

This should return HTML with the login page.

## Ongoing

Pushing to the `main` branch automatically triggers the GitHub Actions workflow to build and deploy.

## Troubleshooting

- **Container not starting**: Check logs with `az containerapp logs show --name <NAME> --resource-group <RG>`.
- **502 errors**: The app uses `min_replicas=0`, so expect a 10-30s cold start delay.
- **Google OAuth mismatch**: Verify the Container App URL is in the Google Cloud authorized origins.
