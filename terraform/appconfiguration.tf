resource "azurerm_app_configuration" "main" {
  name                = "appcs-worldcup"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "free"

  # Disable local (access key) auth — the container app authenticates via managed identity.
  local_auth_enabled = false
}

# Allow the container app's system-assigned identity to read configuration & feature flags.
resource "azurerm_role_assignment" "appcs_data_reader" {
  scope                = azurerm_app_configuration.main.id
  role_definition_name = "App Configuration Data Reader"
  principal_id         = azurerm_container_app.main.identity[0].principal_id
}

# Allow the Terraform principal to manage key-values / feature flags during apply.
data "azurerm_client_config" "current" {}

resource "azurerm_role_assignment" "appcs_data_owner_tf" {
  scope                = azurerm_app_configuration.main.id
  role_definition_name = "App Configuration Data Owner"
  principal_id         = data.azurerm_client_config.current.object_id
}
