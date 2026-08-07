# App_Data

Esta carpeta se usa para datos de aplicación (logs, archivos locales, etc.).

## Credenciales y archivos sensibles

- Los archivos `*.txt` y cualquier archivo cuyo nombre contenga `credentials` en esta carpeta **no deben versionarse** (están en `.gitignore`).
- La aplicación **no escribe** credenciales en archivos en esta carpeta; los secretos se gestionan con `appSettings.secrets.config` (ver raíz del proyecto).
- **En producción** no usar archivos de texto con credenciales aquí; usar únicamente `appSettings.secrets.config` o un secret store (variables de entorno, Azure Key Vault, etc.).
