# SmartLunch API

Backend de SmartLunch: sistema de gestión de comedores empresariales. Maneja
autenticación, usuarios, platos, menú del día, turnos, comedores, un motor de
reglas de bonificación (descuentos configurables sin tocar código) y reportes
de facturación/gestión. Incluye integraciones con SmartTime (datos laborales)
y Biostar (control de acceso).

## Stack

- .NET Framework 4.8.1 · ASP.NET Web API 2
- Entity Framework 6 (Database First)
- SQL Server (probado con SQL Server Express)
- Autenticación JWT (HMAC-SHA256) + refresh tokens
- Swagger para explorar la API (`/swagger`)

## Puesta en marcha

### 1. Credenciales

El proyecto separa los secretos reales de lo que se versiona en Git.

```bash
cp appSettings.secrets.config.example appSettings.secrets.config
```

Editá `appSettings.secrets.config` y completá:

- `SmartTimePassword` — contraseña de la integración SmartTime
- `BiostarPassword` — contraseña de la integración Biostar
- `JwtSecret` — clave para firmar los tokens JWT (larga y aleatoria)
- `ScriptRunnerKey` — clave del panel de scripts pendientes (`/api/DbScripts`)

`appSettings.secrets.config` está en `.gitignore` — **nunca se sube al repo**.
Solo el `.example` (con placeholders) se versiona, como referencia.

### 2. Base de datos

No hace falta correr un script a mano. Al abrir la app por primera vez contra
una base de datos que no existe, se muestra una pantalla de **configuración
inicial** (`Default.html` / `/api/Setup`) que:

1. Crea la base de datos y las tablas (`Scripts/CrearBaseDatosYTablas.sql`).
2. Siembra los catálogos base (planta, plan nutricional, jerarquías, etc.).
3. Pide la contraseña que vas a usar para el usuario `admin` (y para `smarTime`
   si la integración está habilitada) y la deja configurada.

La cadena de conexión se define en `web.config` (`connectionStrings` →
`DataContext`). Por defecto apunta a `.\SQLEXPRESS`.

### 3. Correr la API

Abrir `smartlunch-api.sln` en Visual Studio y ejecutar con IIS Express
(F5), o publicar en un IIS real para producción. La API queda disponible en
la URL que indique el proyecto (por defecto `http://localhost:8000`), con
Swagger en `/swagger`.

### 4. Scripts de migración

Los cambios de esquema posteriores a la instalación inicial viven en
`Scripts/nuevos_scripts/`. Se administran desde el panel `/scripts-pendientes`
del front (o `/api/DbScripts` directamente), que muestra qué scripts faltan
ejecutar contra la base actual y en qué orden. Todo script nuevo también debe
sumarse a `Scripts/CrearBaseDatosYTablas.sql` para que las instalaciones desde
cero ya lo incluyan.

## Estructura

```
Controllers/    Endpoints de la Web API (uno por recurso)
Service/        Lógica de negocio (ServicioX por cada Controller)
Dtos/           Contratos de entrada/salida de la API
Models/         Entidades EF6 (Database First) + DataContext
Filters/        Autenticación por rol, rate limiting, manejo de errores
App_Start/      Configuración de arranque (Web API, Swagger, seed de datos)
Scripts/        Script de creación de base + migraciones incrementales
```

## Seguridad

- Contraseñas con PBKDF2 (salt por usuario, iteraciones versionadas por fila
  para poder subir el costo de hashing sin invalidar logins existentes).
- Rate limiting por IP y por usuario en los endpoints de login.
- CORS restringido por whitelist (`CorsAllowedOrigins` en `web.config`).
- Roles por jerarquía (Admin/Gerencia/Cocina/Comensal) vía JWT, con
  autorización explícita en cada endpoint sensible.

## Advertencias

- ❌ Nunca subir `appSettings.secrets.config` ni `connectionStrings.secrets.config`
  al repositorio (ya están en `.gitignore`).
- 📁 La carpeta `documentos/` (notas internas) está excluida del repo a
  propósito — puede contener datos sensibles de trabajo, no es documentación
  pública del proyecto.
- 🔒 Cada ambiente (desarrollo, staging, producción) debe tener su propio
  archivo de secretos.
