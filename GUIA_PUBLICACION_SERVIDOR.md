# 📦 Guía Completa: Publicación del Proyecto SmartLunch API en Servidor IIS

**Versión:** 1.0  
**Fecha:** 2024  
**Proyecto:** SmartLunch Secure API  
**Framework:** ASP.NET Web API 2 (.NET Framework 4.8.1)

---

## 📋 Índice

1. [Requisitos Previos](#requisitos-previos)
2. [Preparación del Proyecto](#preparación-del-proyecto)
3. [Configuración de IIS](#configuración-de-iis)
4. [Publicación desde Visual Studio](#publicación-desde-visual-studio)
5. [Configuración Post-Despliegue](#configuración-post-despliegue)
6. [Verificación y Pruebas](#verificación-y-pruebas)
7. [Troubleshooting](#troubleshooting)
8. [Checklist Final](#checklist-final)

---

## 1. Requisitos Previos

### 1.1 En el Servidor

- ✅ **Windows Server** (2012 R2 o superior recomendado)
- ✅ **IIS 8.0 o superior** instalado y configurado
- ✅ **.NET Framework 4.8.1** instalado
- ✅ **SQL Server** (versión compatible con Entity Framework 6.5.1)
- ✅ **Acceso de administrador** al servidor
- ✅ **Firewall** configurado para permitir tráfico HTTP/HTTPS

### 1.2 En la Máquina de Desarrollo

- ✅ **Visual Studio 2019 o superior**
- ✅ **Proyecto compilado sin errores**
- ✅ **Acceso al servidor** (RDP, red compartida, etc.)
- ✅ **Credenciales de administrador** del servidor

### 1.3 Verificar IIS y .NET Framework

**En el servidor, ejecutar en PowerShell (como Administrador):**

```powershell
# Verificar IIS
Get-WindowsFeature -Name Web-Server
Get-WindowsFeature -Name Web-Asp-Net45

# Verificar .NET Framework
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\" | Select-Object Version
```

**Si IIS no está instalado:**

```powershell
# Instalar IIS con características necesarias
Install-WindowsFeature -Name Web-Server -IncludeManagementTools
Install-WindowsFeature -Name Web-Asp-Net45
Install-WindowsFeature -Name Web-Net-Ext45
Install-WindowsFeature -Name Web-ISAPI-Ext
Install-WindowsFeature -Name Web-ISAPI-Filter
```

---

## 2. Preparación del Proyecto

### 2.1 Verificar Configuración

**Antes de publicar, verificar:**

1. ✅ **Conexión a Base de Datos:**
   - Abrir `Web.config`
   - Verificar la cadena de conexión `DataContext`
   - Asegurarse de que apunta a la base de datos del servidor

2. ✅ **Archivo de Secretos:**
   - Verificar que `appSettings.secrets.config` existe
   - Contiene todas las credenciales necesarias:
     - `SmartTimePassword`
     - `BiostarPassword`
     - `JwtSecret`
     - `CorsAllowedOrigins`

3. ✅ **Configuración de CORS:**
   - Verificar URLs permitidas en `CorsAllowedOrigins`
   - Asegurarse de incluir las URLs del frontend

### 2.2 Compilar en Modo Release

**En Visual Studio:**

1. Seleccionar **Release** en el dropdown de configuración
2. **Build** → **Rebuild Solution** (o `Ctrl+Shift+B`)
3. Verificar que no hay errores de compilación
4. Verificar que no hay warnings críticos

### 2.3 Verificar Archivos Necesarios

**Asegurarse de que estos archivos estén en el proyecto:**

- ✅ `Web.config`
- ✅ `appSettings.secrets.config` (NO versionado en Git)
- ✅ `appSettings.secrets.config.example` (template)
- ✅ Todos los archivos `.dll` compilados
- ✅ `bin/` con todas las dependencias

---

## 3. Configuración de IIS

### 3.1 Crear Pool de Aplicaciones

**En IIS Manager:**

1. Abrir **IIS Manager**
2. Expandir el servidor
3. Click derecho en **Application Pools** → **Add Application Pool**
4. Configurar:
   - **Name:** `SmartLunchAPI` (o el nombre que prefieras)
   - **.NET CLR Version:** `.NET CLR Version v4.0`
   - **Managed Pipeline Mode:** `Integrated`
5. Click en **OK**

**Configurar el Pool:**

1. Seleccionar el pool `SmartLunchAPI`
2. Click en **Advanced Settings** (o **Basic Settings** → **Advanced Settings**)
3. Configurar:
   - **Start Mode:** `AlwaysRunning` (recomendado)
   - **Idle Timeout:** `0` (para evitar que se detenga)
   - **Maximum Worker Processes:** `1` (o más si usas Web Garden)
   - **Identity:** `ApplicationPoolIdentity` (o cuenta de servicio específica)

### 3.2 Crear Sitio Web

**En IIS Manager:**

1. Click derecho en **Sites** → **Add Website**
2. Configurar:
   - **Site name:** `SmartLunchAPI`
   - **Application pool:** Seleccionar `SmartLunchAPI` (creado anteriormente)
   - **Physical path:** Ruta donde se publicará (ej: `C:\inetpub\wwwroot\SmartLunchAPI`)
   - **Binding:**
     - **Type:** `http` (o `https` si tienes certificado SSL)
     - **IP address:** `All Unassigned` (o IP específica)
     - **Port:** `80` (o `443` para HTTPS)
     - **Host name:** (dejar vacío o poner dominio, ej: `api.smartlunch.com`)
3. Click en **OK**

**Nota:** Si usas HTTPS, necesitarás configurar un certificado SSL.

### 3.3 Configurar Permisos

**Asignar permisos a la carpeta del sitio:**

1. Navegar a la carpeta física del sitio (ej: `C:\inetpub\wwwroot\SmartLunchAPI`)
2. Click derecho → **Properties** → **Security**
3. Click en **Edit** → **Add**
4. Agregar:
   - `IIS_IUSRS` → **Read & Execute**, **List folder contents**, **Read**
   - `IIS AppPool\SmartLunchAPI` → **Read & Execute**, **List folder contents**, **Read**
   - Usuario del Application Pool (si usas cuenta específica)
5. Para la carpeta `App_Data\Logs`, agregar también **Write** permissions

**En PowerShell (como Administrador):**

```powershell
$sitePath = "C:\inetpub\wwwroot\SmartLunchAPI"
$appPoolName = "SmartLunchAPI"

# Dar permisos a IIS_IUSRS
icacls $sitePath /grant "IIS_IUSRS:(OI)(CI)(RX)" /T

# Dar permisos al Application Pool
icacls $sitePath /grant "IIS AppPool\$appPoolName:(OI)(CI)(RX)" /T

# Dar permisos de escritura a App_Data\Logs
$logsPath = Join-Path $sitePath "App_Data\Logs"
if (Test-Path $logsPath) {
    icacls $logsPath /grant "IIS AppPool\$appPoolName:(OI)(CI)(F)" /T
}
```

---

## 4. Publicación desde Visual Studio

### 4.1 Método 1: Publicación por Archivo (Recomendado)

**Pasos:**

1. Click derecho en el proyecto `smartlunch-api` → **Publish**
2. Si ya tienes un perfil:
   - Seleccionar el perfil existente
   - Click en **Publish**
3. Si no tienes perfil:
   - Click en **New Profile**
   - Seleccionar **Folder** → **Next**
   - **Target location:** Ruta local o de red (ej: `\\servidor\c$\inetpub\wwwroot\SmartLunchAPI`)
   - Click en **Finish**
   - Click en **Publish**

### 4.2 Método 2: Publicación Manual (Copia de Archivos)

**Si prefieres copiar manualmente:**

1. **Compilar el proyecto** en modo Release
2. **Copiar los siguientes archivos y carpetas** a la carpeta del sitio en IIS:
   - `bin/` (toda la carpeta)
   - `App_Data/` (si existe, crear si no)
   - `App_Start/`
   - `Controllers/`
   - `Dtos/`
   - `Filters/`
   - `Models/`
   - `Service/`
   - `Services/`
   - `Utils/`
   - `Global.asax`
   - `Global.asax.cs`
   - `Web.config`
   - `appSettings.secrets.config` ⚠️ **IMPORTANTE: No olvidar este archivo**

**Estructura esperada en el servidor:**

```
C:\inetpub\wwwroot\SmartLunchAPI\
├── bin\
├── App_Data\
│   └── Logs\
├── App_Start\
├── Controllers\
├── Dtos\
├── Filters\
├── Models\
├── Service\
├── Services\
├── Utils\
├── Global.asax
├── Global.asax.cs
├── Web.config
└── appSettings.secrets.config  ⚠️ CRÍTICO
```

### 4.3 Verificar Archivos Publicados

**Después de publicar, verificar:**

- ✅ Todos los `.dll` están en `bin/`
- ✅ `Web.config` está presente
- ✅ `appSettings.secrets.config` está presente (NO debe estar en Git)
- ✅ Carpeta `App_Data\Logs` existe y tiene permisos de escritura

---

## 5. Configuración Post-Despliegue

### 5.1 Actualizar Web.config para Producción

**Editar `Web.config` en el servidor:**

1. **Cadena de Conexión:**
   ```xml
   <connectionStrings>
     <add name="DataContext" 
          connectionString="Server=TU_SERVIDOR_SQL;Database=smartlunch_secure;Integrated Security=True;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Timeout=30;" 
          providerName="System.Data.SqlClient" />
   </connectionStrings>
   ```

2. **Configuración de CORS:**
   ```xml
   <add key="CorsAllowedOrigins" value="http://172.16.41.24:8000,http://172.16.41.52:4000" />
   ```
   ⚠️ Ajustar según las URLs reales del frontend

3. **Configuración de Logging:**
   ```xml
   <add key="LogPath" value="~/App_Data/Logs" />
   <add key="LogLevel" value="Information" />
   ```

### 5.2 Crear appSettings.secrets.config

**⚠️ CRÍTICO: Este archivo NO debe estar en Git**

**En el servidor, crear `appSettings.secrets.config` en la raíz del sitio:**

```xml
<?xml version="1.0" encoding="utf-8"?>
<appSettings>
  <!-- Credenciales SmartTime -->
  <add key="SmartTimePassword" value="TU_PASSWORD_SMARTTIME" />
  
  <!-- Credenciales Biostar -->
  <add key="BiostarPassword" value="TU_PASSWORD_BIOSTAR" />
  
  <!-- Secret JWT -->
  <add key="JwtSecret" value="TU_SECRET_JWT_MUY_SEGURO_Y_LARGO" />
  
  <!-- CORS Origins (opcional, también puede ir en Web.config) -->
  <add key="CorsAllowedOrigins" value="http://172.16.41.24:8000,http://172.16.41.52:4000" />
</appSettings>
```

**⚠️ IMPORTANTE:**
- Este archivo contiene credenciales sensibles
- NO debe estar en el repositorio Git
- Debe tener permisos restrictivos (solo lectura para IIS)
- Guardar una copia de respaldo en lugar seguro

### 5.3 Configurar Permisos del Archivo de Secretos

**En PowerShell (como Administrador):**

```powershell
$secretsFile = "C:\inetpub\wwwroot\SmartLunchAPI\appSettings.secrets.config"

# Remover permisos de todos excepto Administradores y SYSTEM
icacls $secretsFile /inheritance:r
icacls $secretsFile /grant "Administrators:(F)"
icacls $secretsFile /grant "SYSTEM:(F)"
icacls $secretsFile /grant "IIS AppPool\SmartLunchAPI:(R)"
```

### 5.4 Verificar Base de Datos

**Asegurarse de que:**

1. ✅ La base de datos `smartlunch_secure` existe en SQL Server
2. ✅ El usuario de IIS tiene permisos para conectarse
3. ✅ Las tablas están creadas (Entity Framework puede crearlas automáticamente)
4. ✅ Hay datos de prueba si es necesario

**Si necesitas crear la base de datos:**

```sql
-- Conectarse a SQL Server como administrador
CREATE DATABASE smartlunch_secure;
GO

USE smartlunch_secure;
GO

-- Entity Framework creará las tablas automáticamente en el primer acceso
-- O puedes ejecutar migraciones si las tienes configuradas
```

### 5.5 Configurar Firewall

**Abrir puertos necesarios:**

```powershell
# Permitir HTTP (puerto 80)
New-NetFirewallRule -DisplayName "SmartLunch API HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow

# Permitir HTTPS (puerto 443) si usas SSL
New-NetFirewallRule -DisplayName "SmartLunch API HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
```

---

## 6. Verificación y Pruebas

### 6.1 Verificar que el Sitio Está Corriendo

**En IIS Manager:**

1. Seleccionar el sitio `SmartLunchAPI`
2. Verificar que el estado es **Started**
3. Si está detenido, click derecho → **Start**

**En PowerShell:**

```powershell
# Verificar estado del sitio
Get-Website -Name "SmartLunchAPI"

# Iniciar el sitio si está detenido
Start-Website -Name "SmartLunchAPI"
```

### 6.2 Probar Endpoint de Health Check

**Abrir navegador o usar PowerShell:**

```powershell
# Probar endpoint de login (debe retornar 400, no 500)
Invoke-WebRequest -Uri "http://localhost/api/login/Autentificar" -Method POST -Body '{}' -ContentType "application/json"

# Probar Swagger
Start-Process "http://localhost/swagger"
```

**Si obtienes error 500, revisar:**

1. **Event Viewer** → **Windows Logs** → **Application** (buscar errores)
2. **Logs de la aplicación** en `App_Data\Logs\`
3. **IIS Manager** → **Failed Request Tracing** (si está habilitado)

### 6.3 Probar Autenticación

**Usar Swagger o Postman:**

1. Abrir `http://tu-servidor/swagger`
2. Probar endpoint `/api/login/Autentificar`
3. Verificar que retorna token JWT
4. Usar el token para probar endpoints protegidos

### 6.4 Verificar Logs

**Revisar logs de la aplicación:**

```powershell
# Ver últimos logs
Get-Content "C:\inetpub\wwwroot\SmartLunchAPI\App_Data\Logs\smartlunch-api-*.log" -Tail 50
```

**Verificar que:**

- ✅ Los logs se están generando
- ✅ No hay errores críticos
- ✅ Las operaciones se registran correctamente

### 6.5 Probar Integración con Biostar

**Si tienes Biostar configurado:**

```powershell
# Probar endpoint de Biostar
Invoke-RestMethod -Uri "http://localhost/api/biostar/events" -Method POST -ContentType "application/json"
```

**Verificar:**

- ✅ Conexión con Biostar funciona
- ✅ Eventos se obtienen correctamente
- ✅ Fichadas se registran en la BD

---

## 7. Troubleshooting

### 7.1 Error 500 - Internal Server Error

**Causas comunes:**

1. **Archivo de secretos faltante:**
   - Verificar que `appSettings.secrets.config` existe
   - Verificar permisos del archivo

2. **Cadena de conexión incorrecta:**
   - Verificar `Web.config` → `connectionStrings`
   - Verificar que SQL Server está accesible
   - Verificar permisos del usuario de IIS en SQL Server

3. **Dependencias faltantes:**
   - Verificar que todos los `.dll` están en `bin/`
   - Verificar que `.NET Framework 4.8.1` está instalado

**Solución:**

```powershell
# Ver errores detallados en Event Viewer
Get-EventLog -LogName Application -Source "ASP.NET*" -Newest 10 | Format-List
```

### 7.2 Error 401 - Unauthorized

**Causas:**

- Token JWT inválido o expirado
- `JwtSecret` incorrecto en `appSettings.secrets.config`

**Solución:**

- Verificar que `JwtSecret` es el mismo usado para generar tokens
- Verificar que el token no haya expirado

### 7.3 Error de CORS

**Causas:**

- URL del frontend no está en `CorsAllowedOrigins`
- Configuración de CORS incorrecta

**Solución:**

- Agregar la URL del frontend a `CorsAllowedOrigins` en `Web.config` o `appSettings.secrets.config`
- Verificar que el formato sea correcto: `http://dominio:puerto`

### 7.4 Error de Conexión a Base de Datos

**Causas:**

- SQL Server no accesible
- Credenciales incorrectas
- Base de datos no existe

**Solución:**

```powershell
# Probar conexión desde el servidor
Test-NetConnection -ComputerName "TU_SERVIDOR_SQL" -Port 1433

# Verificar que la base de datos existe
# (usar SQL Server Management Studio)
```

### 7.5 Logs No Se Generan

**Causas:**

- Carpeta `App_Data\Logs` no existe
- Permisos insuficientes

**Solución:**

```powershell
$logsPath = "C:\inetpub\wwwroot\SmartLunchAPI\App_Data\Logs"
New-Item -ItemType Directory -Path $logsPath -Force
icacls $logsPath /grant "IIS AppPool\SmartLunchAPI:(OI)(CI)(F)"
```

---

## 8. Checklist Final

### Antes de Considerar la Publicación Completa:

- [ ] IIS configurado y sitio creado
- [ ] Application Pool configurado correctamente
- [ ] Archivos publicados en la carpeta del sitio
- [ ] `appSettings.secrets.config` creado con todas las credenciales
- [ ] `Web.config` actualizado con cadena de conexión correcta
- [ ] Permisos de carpetas y archivos configurados
- [ ] Base de datos accesible y con permisos correctos
- [ ] Firewall configurado (puertos 80/443 abiertos)
- [ ] Sitio iniciado en IIS
- [ ] Endpoint de login responde (aunque sea con error 400)
- [ ] Swagger accesible
- [ ] Autenticación funciona (login retorna token)
- [ ] Endpoints protegidos requieren autenticación
- [ ] Logs se generan correctamente
- [ ] Integración con Biostar funciona (si aplica)
- [ ] CORS configurado correctamente
- [ ] Frontend puede conectarse a la API

### Seguridad:

- [ ] `appSettings.secrets.config` NO está en Git
- [ ] Permisos del archivo de secretos son restrictivos
- [ ] Credenciales son seguras (no son las de desarrollo)
- [ ] HTTPS configurado (si es posible)
- [ ] Firewall bloquea puertos innecesarios

---

## 9. Comandos Útiles

### PowerShell - Verificar Estado

```powershell
# Estado del sitio
Get-Website -Name "SmartLunchAPI"

# Estado del Application Pool
Get-WebAppPoolState -Name "SmartLunchAPI"

# Reiniciar Application Pool
Restart-WebAppPool -Name "SmartLunchAPI"

# Ver procesos del Application Pool
Get-Process -Name "w3wp" | Where-Object {$_.Path -like "*SmartLunchAPI*"}
```

### PowerShell - Ver Logs

```powershell
# Ver últimos logs de la aplicación
Get-Content "C:\inetpub\wwwroot\SmartLunchAPI\App_Data\Logs\*.log" -Tail 100

# Buscar errores en logs
Select-String -Path "C:\inetpub\wwwroot\SmartLunchAPI\App_Data\Logs\*.log" -Pattern "ERROR" | Select-Object -Last 20
```

### PowerShell - Reiniciar Sitio

```powershell
# Reiniciar Application Pool (recomendado)
Restart-WebAppPool -Name "SmartLunchAPI"

# O reiniciar el sitio completo
Stop-Website -Name "SmartLunchAPI"
Start-Website -Name "SmartLunchAPI"
```

---

## 10. Mantenimiento Post-Despliegue

### Actualizaciones

**Para actualizar la aplicación:**

1. Compilar nueva versión en modo Release
2. Detener el Application Pool (opcional, para evitar errores durante la actualización)
3. Copiar nuevos archivos (mantener `appSettings.secrets.config`)
4. Reiniciar el Application Pool
5. Verificar que todo funciona

### Backup

**Realizar backups regulares de:**

- Base de datos `smartlunch_secure`
- Archivo `appSettings.secrets.config`
- Logs importantes
- Configuración de IIS (exportar sitio)

### Monitoreo

**Monitorear regularmente:**

- Logs de la aplicación (`App_Data\Logs\`)
- Event Viewer → Application Logs
- Performance del Application Pool
- Uso de memoria y CPU
- Conexiones a la base de datos

---

## 📞 Soporte

Si encuentras problemas durante la publicación:

1. Revisar los logs en `App_Data\Logs\`
2. Revisar Event Viewer → Application Logs
3. Verificar configuración en `Web.config`
4. Verificar que todas las dependencias están presentes

---

**Fin del Documento**

