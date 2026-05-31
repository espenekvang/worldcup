resource "azurerm_mssql_server" "main" {
  name                = "sql-worldcup-ekvang"
  resource_group_name = azurerm_resource_group.main.name
  location            = "westeurope"
  version             = "12.0"

  azuread_administrator {
    login_username              = var.sql_admin_email
    object_id                   = var.sql_admin_object_id
    azuread_authentication_only = true
  }
}

# NOTE: After provisioning, apply the "Free offer" via the Azure Portal:
# Database > Compute + Storage > Free database offer > Apply
resource "azurerm_mssql_database" "main" {
  name      = "sqldb-worldcup"
  server_id = azurerm_mssql_server.main.id

  sku_name                    = "GP_S_Gen5_1"
  min_capacity                = 0.5
  auto_pause_delay_in_minutes = 60
  max_size_gb                 = 32
  zone_redundant              = false
  storage_account_type        = "Local"

  short_term_retention_policy {
    retention_days = 7
  }

  lifecycle {
    ignore_changes = [sku_name]
  }
}

# Allow Azure services (Container Apps) to reach the SQL server
resource "azurerm_mssql_firewall_rule" "allow_azure" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# NOTE: After provisioning, grant the Container App's managed identity access to the database:
#   CREATE USER [ca-worldcup] FROM EXTERNAL PROVIDER;
#   ALTER ROLE db_datareader ADD MEMBER [ca-worldcup];
#   ALTER ROLE db_datawriter ADD MEMBER [ca-worldcup];
#   ALTER ROLE db_ddladmin ADD MEMBER [ca-worldcup];
