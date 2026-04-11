resource "azurerm_storage_account" "main" {
  name                     = "stworldcup${substr(md5(var.resource_group_name), 0, 8)}"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_storage_share" "backup" {
  name               = "worldcup-backup"
  storage_account_id = azurerm_storage_account.main.id
  quota              = 1
}
