resource "azurerm_container_app_environment" "main" {
  name                       = "cae-worldcup"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
}

resource "azurerm_container_app_environment_storage" "backup" {
  name                         = "backup-storage"
  container_app_environment_id = azurerm_container_app_environment.main.id
  account_name                 = azurerm_storage_account.main.name
  share_name                   = azurerm_storage_share.backup.name
  access_key                   = azurerm_storage_account.main.primary_access_key
  access_mode                  = "ReadWrite"
}

resource "azurerm_container_app" "main" {
  name                         = var.container_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"

  identity {
    type = "SystemAssigned"
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = "System"
  }

  secret {
    name  = "jwt-key"
    value = var.jwt_key
  }

  secret {
    name  = "google-client-id"
    value = var.google_client_id
  }

  secret {
    name  = "admin-email"
    value = var.admin_email
  }

  template {
    min_replicas = 0
    max_replicas = 1

    volume {
      name         = "backup-volume"
      storage_type = "AzureFile"
      storage_name = azurerm_container_app_environment_storage.backup.name
    }

    container {
      name   = "worldcup"
      image  = "mcr.microsoft.com/k8se/quickstart:latest"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name        = "Jwt__Key"
        secret_name = "jwt-key"
      }

      env {
        name  = "Jwt__Issuer"
        value = var.jwt_issuer
      }

      env {
        name  = "Jwt__Audience"
        value = var.jwt_audience
      }

      env {
        name        = "Google__ClientId"
        secret_name = "google-client-id"
      }

      env {
        name        = "Admin__Email"
        secret_name = "admin-email"
      }

      env {
        name  = "ConnectionStrings__DefaultConnection"
        value = "Data Source=/app/data/worldcup.db"
      }

      env {
        name  = "Backup__Path"
        value = "/mnt/backup"
      }

      volume_mounts {
        name = "backup-volume"
        path = "/mnt/backup"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 80
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image,
      ingress[0].target_port,
    ]
  }
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_container_app.main.identity[0].principal_id
}
