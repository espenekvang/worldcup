output "container_app_url" {
  description = "URL of the deployed Container App"
  value       = "https://${azurerm_container_app.main.ingress[0].fqdn}"
}

output "acr_login_server" {
  description = "ACR login server URL"
  value       = azurerm_container_registry.main.login_server
}

output "resource_group_name" {
  description = "Name of the Azure resource group"
  value       = azurerm_resource_group.main.name
}

output "custom_domain" {
  description = "Custom domain for the Container App"
  value       = var.custom_domain != "" ? "https://${var.custom_domain}" : null
}

output "custom_domain_verification_id" {
  description = "TXT record value for domain verification (set as asuid.<subdomain> record)"
  value       = azurerm_container_app.main.custom_domain_verification_id
  sensitive   = true
}
