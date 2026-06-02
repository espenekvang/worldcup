variable "resource_group_name" {
  description = "Name of the Azure resource group"
  type        = string
  default     = "rg-worldcup"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "northeurope"
}

variable "acr_name" {
  description = "Name of the Azure Container Registry (must be globally unique)"
  type        = string
}

variable "container_app_name" {
  description = "Name of the Container App"
  type        = string
  default     = "ca-worldcup"
}

variable "jwt_key" {
  description = "JWT signing key (minimum 32 characters)"
  type        = string
  sensitive   = true
}

variable "jwt_issuer" {
  description = "JWT issuer"
  type        = string
  default     = "worldcup"
}

variable "jwt_audience" {
  description = "JWT audience"
  type        = string
  default     = "worldcup"
}

variable "google_client_id" {
  description = "Google OAuth client ID"
  type        = string
  sensitive   = true
}

variable "admin_email" {
  description = "Email address of the admin user"
  type        = string
}

variable "vite_google_client_id" {
  description = "Google OAuth client ID for frontend build (baked into JS bundle)"
  type        = string
}

variable "wc2026_api_key" {
  description = "API key for the external WC2026 results/fixtures API"
  type        = string
  sensitive   = true
  default     = ""
}

variable "custom_domain" {
  description = "Custom domain for the Container App (e.g. vm.ekvang.no)"
  type        = string
  default     = ""
}

variable "sql_admin_email" {
  description = "Email/UPN of the Entra ID SQL admin"
  type        = string
}

variable "sql_admin_object_id" {
  description = "Object ID of the Entra ID SQL admin"
  type        = string
}

variable "slack_webhook_url" {
  description = "Slack incoming webhook URL for error/warning log notifications"
  type        = string
  sensitive   = true
  default     = ""
}
