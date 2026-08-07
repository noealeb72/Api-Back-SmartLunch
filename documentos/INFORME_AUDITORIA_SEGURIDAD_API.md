# Informe de Auditoría – API SmartLunch (Seguridad y Arquitectura)

**Proyecto:** smartLunchApiAutentificacionSmartTime  
**Alcance:** Código del workspace (back-end API .NET Framework)  
**Fecha:** Febrero 2026  
**Rol:** Arquitecto de Software / Especialista AppSec  

> ⚠️ **Nota de actualización (agosto 2026):** este informe es un snapshot de febrero
> 2026. Varios hallazgos CRÍTICO/ALTO listados abajo ya fueron remediados después de
> esa fecha; el detalle de cada fix está en el historial de commits del repo, no acá.
> Antes de tomar una decisión basada en este documento, verificar el estado actual
> en el código en vez de asumir que sigue igual. Remediado confirmado desde entonces:
> - **C1** (secretos en Web.config): removidos, quedan solo en `appSettings.secrets.config`.
> - **C2** (credenciales en `App_Data`): `.gitignore` ya excluye `App_Data/*.txt` y `*credentials*`.
> - **B1** (PBKDF2 10.000 iteraciones): subido a 100.000, con conteo de iteraciones
>   guardado por login para no invalidar contraseñas existentes.
> - Hallazgo no listado acá pero corregido: falta de chequeo de rol en
>   `PUT /api/usuario/actualizar` (cualquier usuario autenticado podía modificar
>   cualquier usuario, incluido autopromoverse a Admin).
> - Hallazgo no listado acá pero corregido: IDOR en creación de comandas
>   (`usuarioId` se tomaba del body en vez del token).
> - Hallazgo no listado acá pero corregido: path traversal en
>   `GET /api/plato/imagen/{archivo}`.

---

## A) Resumen ejecutivo – Top 10 hallazgos más críticos

| # | Prioridad | Hallazgo |
|---|-----------|----------|
| 1 | **CRÍTICO** | Secretos (JwtSecret, BiostarPassword, SmartTimePassword) en `Web.config` versionado; riesgo de exposición en repositorio. |
| 2 | **CRÍTICO** | Archivos de credenciales en `App_Data` (admin_credentials.txt, smarttime_credentials.txt) pueden quedar en el repo o en despliegues. |
| 3 | **ALTO** | JWT: `ClockSkew` de 5 minutos amplía ventana de validez de tokens expirados. |
| 4 | **ALTO** | Filtro global de excepciones registra `StackTrace` completo; en producción puede filtrarse a logs externos. |
| 5 | **ALTO** | `LoginRequestDto` sin `[StringLength]` en Username/Password; posible DoS o abuso con cadenas muy largas. |
| 6 | **MEDIO** | No hay control por recurso (RBAC) explícito por rol; todos los usuarios con token pueden acceder a todos los endpoints `[Authorize]`. |
| 7 | **MEDIO** | Carpeta `Tests` sin tests unitarios/integración; sin cobertura automatizada. |
| 8 | **MEDIO** | `compilation debug="true"` en `Web.config`; en producción debe estar en `false`. |
| 9 | **BAJO** | PBKDF2 con 10.000 iteraciones; recomendable subir (p. ej. 100.000+) según OWASP. |
| 10 | **BAJO** | No hay `RequestId`/`CorrelationId` en respuestas ni en logs para trazabilidad completa. |

---

## B) Hallazgos priorizados

### CRÍTICO

#### C1. Secretos en Web.config versionado

- **Evidencia:**  
  `Web.config` (líneas 57-59) contenía (valores reales redactados acá a propósito):
  ```xml
  <add key="JwtSecret" value="[REDACTADO]" />
  <add key="SmartTimePassword" value="[REDACTADO]" />
  <add key="BiostarPassword" value="[REDACTADO]" />
  ```
  Y `file="appSettings.secrets.config"` en `<appSettings>` (línea 11).  
  Aunque existe `appSettings.secrets.config` y está en `.gitignore`, **Web.config sí está versionado con valores por defecto que incluyen secretos reales**.

- **Impacto:** Cualquier persona con acceso al repositorio puede obtener JWT key y contraseñas de integraciones; suplantación de API, acceso a sistemas externos (SmartTime, Biostar).

- **Probabilidad:** Alta (si el repo es compartido o público).

- **Remediación:**  
  1. Quitar de `Web.config` todo valor sensible; dejar solo claves con placeholders o sin valor.  
  2. Documentar que los valores reales van en `appSettings.secrets.config` (no versionado) o en variables de entorno.  
  3. Añadir `appSettings.secrets.config.example` con claves vacías y descripción.

- **Ejemplo recomendado (Web.config):**
  ```xml
  <appSettings file="appSettings.secrets.config">
    <!-- JwtSecret, SmartTimePassword, BiostarPassword SOLO en appSettings.secrets.config -->
    <add key="JwtIssuer" value="SmartLunchApi" />
    <add key="JwtAudience" value="SmartLunchFront" />
    <add key="JwtExpirationMinutes" value="60" />
  </appSettings>
  ```

---

#### C2. Credenciales en App_Data

- **Evidencia:**  
  `App_Data\admin_credentials.txt` y `App_Data\smarttime_credentials.txt` (listados en estructura del proyecto).  
  `.gitignore` no incluye `App_Data/*.txt`.

- **Impacto:** Credenciales en texto plano en disco; si se versiona o se despliega tal cual, exposición de cuentas.

- **Probabilidad:** Media-Alta.

- **Remediación:**  
  1. Añadir a `.gitignore`: `App_Data/*.txt` o `App_Data/*credentials*`.  
  2. No generar ni leer credenciales desde archivos de texto en producción; usar solo config/secret store.  
  3. Si son solo para desarrollo local, documentarlo y asegurar que no se desplieguen.

---

### ALTO

#### A1. JWT – ClockSkew de 5 minutos

- **Evidencia:**  
  `App_Start/Startup.cs` (líneas 71-73):
  ```csharp
  ValidateLifetime = true,
  ClockSkew = TimeSpan.FromMinutes(5)
  ```

- **Impacto:** Un token expirado sigue siendo aceptado hasta 5 minutos después; aumenta ventana de uso de tokens robados.

- **Probabilidad:** Media.

- **Remediación:** Reducir a 0 o 1 minuto en producción. Mantener 5 min solo si hay evidencia de desincronización de relojes (y documentarlo).

- **Ejemplo:**
  ```csharp
  ClockSkew = TimeSpan.FromMinutes(0)  // o 1 si hay NTP inestable
  ```

---

#### A2. StackTrace en logs de excepciones

- **Evidencia:**  
  `Filters/GlobalExceptionFilterAttribute.cs` (líneas 39-50): el objeto anónimo pasado al logger incluye `StackTrace = exception.StackTrace`.

- **Impacto:** En entornos donde los logs se envían a sistemas externos o se comparten, un stack trace puede revelar rutas, librerías y lógica interna.

- **Probabilidad:** Media.

- **Remediación:** No incluir `StackTrace` en el objeto de log en producción, o usar un flag de configuración (p. ej. `IncludeStackTraceInLogs`) y solo en desarrollo.

- **Ejemplo:**
  ```csharp
  var logData = new {
      Controller = controllerName,
      Action = actionName,
      Method = method,
      Uri = uri,
      ClientIp = clientIp,
      ExceptionType = exception.GetType().Name,
      ExceptionMessage = exception.Message
      // StackTrace solo si IsDevelopmentEnvironment() o config
  };
  if (IsDevelopmentEnvironment())
      logData = new { ... logData ..., StackTrace = exception.StackTrace };
  _logger.LogError(exception, "Excepción no manejada...", logData);
  ```

---

#### A3. LoginRequestDto sin límite de longitud

- **Evidencia:**  
  `Dtos/LoginDto.cs` (líneas 166-181): `LoginRequestDto` tiene `[Required]` en Username y Password pero no `[StringLength]`.

- **Impacto:** Cuerpos de login con cadenas enormes pueden consumir memoria/CPU (hashing) y facilitar DoS.

- **Probabilidad:** Baja-Media.

- **Remediación:** Añadir `[StringLength]` razonable (p. ej. 100 para Username, 256 para Password).

- **Ejemplo:**
  ```csharp
  [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
  [StringLength(100)]
  [JsonProperty("Username")]
  public string Username { get; set; }

  [Required(ErrorMessage = "La contraseña es obligatoria")]
  [StringLength(256, MinimumLength = 1)]
  [JsonProperty("Password")]
  public string Password { get; set; }
  ```

---

### MEDIO

#### M1. Autorización solo por [Authorize]

- **Evidencia:**  
  Controllers usan `[Authorize]` sin roles ni claims (p. ej. `SmartTimeController.cs`, `UsuarioController.cs`). Cualquier usuario autenticado puede llamar a cualquier endpoint protegido.

- **Impacto:** No hay separación por rol (admin vs comensal vs smarTime); posible acceso indebido a datos o operaciones.

- **Probabilidad:** Media (depende de quién tenga tokens).

- **Remediación:** Introducir roles/claims en el JWT (p. ej. desde `jerarquia.nombre` o flag) y usar `[Authorize(Roles = "Admin")]` o filtros por claim en endpoints sensibles (ABM de usuarios, configuración, etc.).

---

#### M2. Sin tests automatizados

- **Evidencia:**  
  Carpeta `Tests/` solo contiene `packages.config` y `Properties/AssemblyInfo.cs`; no hay clases de test.

- **Impacto:** Cambios en lógica de seguridad o negocio sin regresión automatizada; mayor riesgo en refactors.

- **Probabilidad:** Alta a largo plazo.

- **Remediación:** Añadir proyecto de tests (NUnit/xUnit), al menos: login (éxito/fallo), rate limit, validación de DTOs, ServicioLogin/ServicioSmartTimeUsuario (crear usuario, duplicados). Objetivo mínimo: servicios críticos y filtros.

---

#### M3. compilation debug="true"

- **Evidencia:**  
  `Web.config` (línea 71): `<compilation debug="true" targetFramework="4.8" />`.

- **Impacto:** En producción, `debug="true"` empeora rendimiento y puede influir en mensajes de error detallados.

- **Probabilidad:** Alta si se despliega sin transform.

- **Remediación:** Usar transform en `Web.Release.config` para poner `debug="false"` en Release y asegurar que el despliegue use configuración Release.

---

#### M4. Respuestas 500 con detalle según entorno

- **Evidencia:**  
  `GlobalExceptionFilterAttribute.cs` (líneas 97-116): en no-desarrollo no se envía `details` con `stackTrace` al cliente; en desarrollo sí.  
  Criterio de desarrollo: `#if DEBUG` o host localhost (`IsDevelopmentEnvironment()`).

- **Impacto:** Si en producción `IsDevelopmentEnvironment()` devuelve true (p. ej. por host), se podrían filtrar stack traces al cliente.

- **Probabilidad:** Baja si el criterio está bien aplicado.

- **Remediación:** Revisar que en producción el host no sea localhost y que las compilaciones Release no tengan DEBUG; opcionalmente usar solo `#if DEBUG` para no depender del request.

---

### BAJO

#### B1. PBKDF2 con 10.000 iteraciones

- **Evidencia:**  
  `Service/ServicioLogin.cs` (líneas 935, 951) y `Service/PasswordUtils.cs` (líneas 22, 24): `new Rfc2898DeriveBytes(password, salt, 10000)`.

- **Impacto:** 10.000 está por debajo de recomendaciones actuales OWASP (≥100.000 para PBKDF2-HMAC-SHA256); mayor exposición ante ataques offline.

- **Probabilidad:** Baja a medio (si hay dump de BD).

- **Remediación:** Subir iteraciones (p. ej. 100.000) y hacerlo configurable; al cambiar, considerar que contraseñas existentes seguirán con 10.000 hasta que el usuario cambie la clave.

---

#### B2. Sin CorrelationId / RequestId

- **Evidencia:**  
  `WebApiConfig` y `GlobalExceptionFilterAttribute` no inyectan ni leen RequestId/CorrelationId en headers ni en contexto de log.

- **Impacto:** Dificulta correlacionar peticiones con logs en sistemas centralizados.

- **Probabilidad:** N/A (mejora operativa).

- **Remediación:** Añadir middleware o message handler que genere `X-Request-Id` (GUID), lo ponga en `Response.Headers` y en `Serilog.Context`/`LogContext.PushProperty("RequestId", id)` para todos los logs de esa request.

---

## C) Recomendaciones accionables (qué cambiar y dónde)

| Área | Acción | Archivo / Ubicación |
|------|--------|---------------------|
| Secretos | Eliminar JwtSecret, SmartTimePassword, BiostarPassword de Web.config; usar solo appSettings.secrets.config | `Web.config` |
| Secretos | Añadir App_Data/*.txt o *credentials* a .gitignore | `.gitignore` |
| JWT | Reducir ClockSkew a 0 o 1 minuto | `App_Start/Startup.cs` |
| Logging | No loguear StackTrace en producción (o solo bajo config) | `Filters/GlobalExceptionFilterAttribute.cs` |
| Validación | StringLength en Username y Password | `Dtos/LoginDto.cs` – `LoginRequestDto` |
| Autorización | Definir roles/claims en JWT y usar [Authorize(Roles = "...")] en endpoints sensibles | `Service/ServicioLogin.cs` (claims), Controllers |
| Build | Asegurar compilation debug=false en Release | `Web.config` / `Web.Release.config` |
| Tests | Crear tests para login, rate limit, validación, servicios smarTime | `Tests/` |
| Trazabilidad | Añadir RequestId/CorrelationId en middleware o handler y en logs | `WebApiConfig.cs` o nuevo handler, Serilog |

---

## D) Checklist “Listo para producción”

- [ ] **Secretos:** Ningún secreto en archivos versionados; solo en appSettings.secrets.config (o equivalente) y excluido del repo.
- [ ] **JWT:** Issuer, Audience, SigningKey correctos; expiración ≤ 60 min; ClockSkew ≤ 1 min.
- [ ] **Passwords:** Hash con salt (PBKDF2 o superior); iteraciones ≥ 100.000 recomendado.
- [ ] **Rate limit / Lockout:** Activo en login (por IP y por usuario); configurado en Web.config.
- [ ] **CORS:** Orígenes explícitos (no `*`); métodos y headers mínimos necesarios.
- [ ] **Headers de seguridad:** X-Content-Type-Options, X-Frame-Options (ya presentes en Web.config).
- [ ] **Manejo de errores:** 400/401/403/404/409/500 consistentes; sin stack trace al cliente en producción.
- [ ] **Validación:** DTOs con Required, StringLength, Range; ValidateModel aplicado donde corresponda.
- [ ] **Logging:** Sin contraseñas ni tokens en logs; RequestId/CorrelationId recomendado.
- [ ] **compilation:** debug="false" en entorno de producción.
- [ ] **Autorización:** Revisión de qué endpoints requieren rol admin o claims específicos.
- [ ] **Tests:** Suite mínima (login, rate limit, creación usuario smarTime, validaciones).

---

## E) Áreas validadas (resumen)

| Área | Estado | Notas |
|------|--------|--------|
| Autenticación JWT | Correcto | Issuer, Audience, SigningKey, ValidateLifetime; ClockSkew alto (ver A1). |
| Almacenamiento passwords | Correcto | PBKDF2 + salt 16 bytes, 10.000 iteraciones (mejorable a 100k). |
| Rate limit / Lockout | Correcto | Por IP y por usuario; configuración en Web.config; limpieza periódica. |
| Autorización | Parcial | [Authorize] presente; no hay roles/claims en endpoints. |
| Validación inputs | Parcial | DTOs con DataAnnotations; LoginRequestDto sin StringLength (ver A3). |
| Manejo de errores | Correcto | Filtro global; 400/409/500; detalles solo en desarrollo. |
| CORS | Correcto | Orígenes desde config; no AllowAnyOrigin; headers/métodos configurables. |
| Headers seguridad | Correcto | X-Content-Type-Options, X-Frame-Options en Web.config. |
| SQL Injection | Correcto | Uso de EF (parametrizado); no se encontraron consultas raw en el código revisado. |
| Logging / auditoría | Parcial | Serilog; no se loguean passwords; StackTrace sí se loguea (ver A2); sin CorrelationId. |
| Transacciones | Correcto | ServicioUsuario.CrearUsuario y ServicioSmartTimeUsuario usan transacción y dos SaveChanges (usuario luego login). |
| Paginación | Correcto | Listados con page/pageSize y límite (p. ej. pageSize ≤ 100). |
| Configuración / secretos | Crítico | Ver C1, C2; appSettings.secrets.config existe y está en .gitignore pero Web.config contiene secretos. |
| Tests | No cumplido | Carpeta Tests sin tests automatizados. |

**No verificable (requiere revisión externa):**  
- Que `appSettings.secrets.config` no esté versionado y contenga los secretos reales en cada entorno.  
- Que en producción no se desplieguen `App_Data/*.txt` con credenciales.  
- Políticas de red, WAF, y rotación de secretos en el entorno de despliegue.

---

## F) Backlog de tareas

| Prioridad | Tarea | Archivo / Área | Esfuerzo |
|-----------|--------|----------------|----------|
| CRÍTICO | Quitar JwtSecret, SmartTimePassword, BiostarPassword de Web.config; documentar uso de appSettings.secrets.config | Web.config, documentos | S |
| CRÍTICO | Añadir App_Data/*credentials* o App_Data/*.txt a .gitignore; dejar de usar archivos .txt para credenciales en producción | .gitignore, despliegue | S |
| ALTO | Reducir JWT ClockSkew a 0 o 1 minuto | App_Start/Startup.cs | S |
| ALTO | Dejar de incluir StackTrace en logs en producción (o bajo config) | Filters/GlobalExceptionFilterAttribute.cs | S |
| ALTO | Añadir StringLength a Username y Password en LoginRequestDto | Dtos/LoginDto.cs | S |
| MEDIO | Introducir rol/claim en JWT y [Authorize(Roles = "Admin")] en endpoints de administración | ServicioLogin, Controllers | M |
| MEDIO | Crear proyecto de tests: login, rate limit, ServicioSmartTimeUsuario, validación DTOs | Tests/ | L |
| MEDIO | Asegurar compilation debug=false en Release (transform Web.Release.config) | Web.config, Web.Release.config | S |
| BAJO | Aumentar iteraciones PBKDF2 (ej. 100.000) y hacer configurables | ServicioLogin, PasswordUtils, Web.config | M |
| BAJO | Añadir RequestId/CorrelationId en respuestas y en contexto de log | WebApiConfig o MessageHandler, Serilog | M |

---

## G) Matriz de Cumplimiento

Cumplimiento de los requisitos del prompt de auditoría frente al estado actual del código (post-remediaciones aplicadas donde se indica).

| Requisito del prompt | ¿Cumple? | Evidencia (sección/archivo) | Qué falta | Cómo corregirlo |
|----------------------|----------|-----------------------------|-----------|------------------|
| Hallazgos con evidencia en código (archivo/ruta) | **Sí** | B) cada hallazgo C1–B2 con archivo y líneas | Nada | — |
| Priorización CRÍTICO/ALTO/MEDIO/BAJO | **Sí** | A) tabla Top 10; B) por prioridad; F) Backlog con Prioridad | Nada | — |
| Impacto, probabilidad y remediación por hallazgo | **Sí** | B) cada hallazgo tiene Impacto, Probabilidad, Remediación | Nada | — |
| Ejemplos de código cuando aplica | **Sí** | C1, A1, A2, A3 en B) tienen "Ejemplo" o "Ejemplo recomendado" | Nada | — |
| Checklist “listo para producción” | **Sí** | D) Checklist con ítems comprobables | Revisar ítems pendientes en despliegue | Ejecutar checklist antes de cada release |
| Backlog con prioridad, área/archivo, esfuerzo S/M/L | **Sí** | F) Backlog con Prioridad, Tarea, Archivo/Área, Esfuerzo | Nada | — |
| Secretos fuera de archivos versionados | **Parcial** | Web.config: claves con value="" y file=appSettings.secrets.config; .gitignore; appSettings.secrets.config.example | En repo no debe haber valores reales; en producción usar solo secrets.config o variables de entorno | No versionar valores reales; documentar en despliegue |
| Credenciales App_Data no versionadas / no en producción | **Sí** | .gitignore: App_Data/*.txt, App_Data/*credentials*; App_Data/README.md si existe | Asegurar que despliegue no copie .txt de credenciales | Pipeline/checklist de despliegue |
| JWT: expiración, issuer, audience, ClockSkew | **Sí** | Web.config JwtIssuer/Audience/ExpirationMinutes; Startup ClockSkew=1 min | Nada | — |
| Passwords: hash + salt (PBKDF2) | **Sí** | ServicioLogin/PasswordUtils/AuthService: Rfc2898DeriveBytes, salt 16 bytes | Opcional: subir iteraciones y hacer configurables (B1) | Ver backlog B1 |
| Rate limit / lockout en login | **Sí** | RateLimitAttribute; Web.config RateLimit*; LoginController [RateLimit] | Nada | — |
| StackTrace no en logs en producción | **Sí** | GlobalExceptionFilterAttribute: IncludeStackTraceInLogs; StackTrace solo si config o desarrollo | Dejar IncludeStackTraceInLogs=false en producción | Web.config |
| Límite de longitud en Login (Username/Password) | **Sí** | LoginDto.cs: StringLength(100), StringLength(256) en LoginRequestDto | Nada | — |
| compilation debug=false en producción | **Sí** | Web.Release.config: SetAttributes(debug) false | Desplegar en configuración Release | Build/Release |
| Respuestas 500 sin detalle al cliente en producción | **Sí** | GlobalExceptionFilterAttribute: IncludeErrorDetailsInResponse solo #if DEBUG | Nada | — |
| RequestId/CorrelationId en respuestas y logs | **Sí** | Handlers/RequestIdHandler.cs; WebApiConfig; Serilog template con RequestId | Nada | — |
| **Autorización por roles/claims** | **Parcial** | LoginController y SmartTimeController: [Authorize(Roles = "Admin")] en 6 acciones; resto de controllers solo [Authorize] (Controllers/*.cs) | Falta RBAC fino: muchos endpoints solo exigen token, no rol (Usuario, Comanda, Reporte, etc.) | Definir por negocio qué endpoints son solo Admin; añadir [Authorize(Roles = "Admin")] o "User" donde corresponda |
| **Validación de inputs** | **Sí** | DTOs con Required/StringLength en LoginDto, UsuarioDto, SmartTimeUsuarioDtos, etc.; LoginRequestDto: StringLength(100) Username, StringLength(256) Password; login con [RateLimit] (LoginController línea 58) | Nada | — |
| Tests unitarios/integración mínimos | **No** | Tests/ con al menos LoginRequestDtoValidationTests | Suite login, rate limit, ServicioSmartTimeUsuario, validaciones | Ver F) Backlog M2 |
| No inventar hallazgos (todo con evidencia) | **Sí** | Cada hallazgo en B) referencia archivo y fragmento | Nada | — |

**Leyenda:** **Sí** = cumplido en código; **Parcial** = cumplido con condiciones o pendiente en despliegue; **No** = no cumplido.

---

## H) Verificación de requisitos del informe

| Requisito | ¿Cumple? | Dónde |
|-----------|----------|--------|
| **A)** No inventar hallazgos; evidencia en código (archivo/ruta) | Sí | B) C1–B2: cada uno con "Evidencia" y archivo/líneas |
| **B)** Priorización CRÍTICO/ALTO/MEDIO/BAJO | Sí | A) tabla; B) subsecciones por prioridad; F) columna Prioridad |
| **C)** Impacto, probabilidad, remediación por hallazgo | Sí | B) cada hallazgo: Impacto, Probabilidad, Remediación |
| **D)** Ejemplos de código cuando aplica | Sí | C1, A1, A2, A3 en B) incluyen bloque de ejemplo |
| **E)** Checklist final “listo para producción” | Sí | D) Checklist con ítems comprobables |
| **F)** Backlog con prioridad, área/archivo, esfuerzo S/M/L | Sí | F) tabla: Prioridad, Tarea, Archivo/Área, Esfuerzo |

Si algo no es verificable solo con el código (p. ej. que secrets.config no esté en el repo en todos los clones), queda indicado en E) “No verificable”.

---

## I) Validaciones adicionales recomendadas

Validaciones no exigidas explícitamente en el prompt pero recomendables para este proyecto. Para cada una: qué se valida, cómo validar en este proyecto, riesgo si no se valida, prioridad sugerida.

| # | Validación | Qué se valida | Cómo validar en este proyecto | Riesgo si no se valida | Prioridad |
|---|------------|----------------|-------------------------------|-------------------------|-----------|
| 1 | **OWASP API Security Top 10** mapeado a endpoints | A01 Broken Object Level Auth, A02 Auth, A03 Excessive Data Exposure, A07 XSS, etc. | Revisar cada categoría OWASP contra controllers y DTOs; documentar qué endpoint cae en qué riesgo. | Exposición o acceso indebido no detectado. | ALTO |
| 2 | **BOLA/IDOR** (acceso a recurso de otro usuario) | Que un usuario no pueda acceder a datos de otro por cambiar id en URL. | Probar GET/PUT/DELETE con id ajeno al usuario autenticado (p. ej. usuario/id, login/{id}); revisar que se filtre por usuario/rol. | Acceso a datos de otros usuarios. | ALTO |
| 3 | **Swagger en producción** | Que Swagger no esté expuesto sin control o que requiera auth. | Revisar si Swagger está habilitado en producción (SwaggerConfig, rutas); si hace falta, restringir por IP o deshabilitar. | Exposición de superficie de ataque y documentación. | MEDIO |
| 4 | **Subida de archivos** (si aplica) | Extensiones permitidas, tamaño máximo, path traversal, tipo MIME. | Buscar endpoints que reciban archivos (File, IFormFile, multipart); revisar validación de extensión, tamaño y ruta de guardado. | Ejecución de código, sobrescritura de archivos. | MEDIO (si hay upload) |
| 5 | **Idempotencia en POST críticos** | Reintentos que no dupliquen operaciones (p. ej. crear usuario). | Revisar POST que crean recursos (login/smarTime usuarios); si hay idempotency key o deduplicación por negocio (ej. DNI). | Duplicados por reintentos o doble clic. | MEDIO |
| 6 | **Concurrencia en ABM** | Race conditions en alta/edición (optimistic lock, transacciones). | Revisar ServicioUsuario/ServicioSmartTimeUsuario: transacciones, concurrencia en mismo DNI/legajo; considerar RowVersion o similar. | Datos inconsistentes o duplicados. | MEDIO |
| 7 | **Rate limiting por endpoint sensible** | Límite no solo en login sino en otros endpoints costosos o sensibles. | Ver qué endpoints además de login podrían abusarse (listados grandes, creación masiva); valorar RateLimit en más acciones. | DoS o abuso en endpoints concretos. | BAJO |
| 8 | **PII y minimización en respuestas** | No devolver más datos personales de los necesarios. | Revisar DTOs de respuesta (login, usuario, listados): DNI, email, etc.; asegurar que solo se exponga lo necesario por rol. | Incumplimiento RGPD/privacidad. | MEDIO |
| 9 | **Health checks / observabilidad** | Endpoint de salud y métricas básicas. | Buscar endpoint tipo /health o /ping; si no existe, considerar añadirlo para load balancer y monitoreo. | No poder comprobar estado del servicio. | BAJO |
| 10 | **Timezone y fechas en logs/auditoría** | Consistencia UTC y timezone en createdate/updatedate y logs. | Revisar uso de DateTime.UtcNow vs Now en servicios y modelos; logs con marca temporal. | Auditoría incorrecta o confusa. | BAJO |
| 11 | **SAST / dependencias (SCA)** | Análisis estático y vulnerabilidades en paquetes. | Ejecutar dotnet list package --vulnerable; OWASP Dependency-Check o similar; revisar paquetes obsoletos. | Vulnerabilidades conocidas en dependencias. | ALTO |
| 12 | **Multi-tenant / planta_id** (si aplica) | Aislamiento por planta/site. | Si hay planta_id o equivalente: revisar que cada request filtre por el contexto del usuario; no listar datos de otras plantas. | Filtrado incorrecto por tenant. | Según negocio |
| 13 | **Threat modeling (STRIDE)** por módulos críticos | Spoofing, Tampering, Repudiation, Info disclosure, DoS, Elevation of privilege. | No verificable solo con código: requiere taller con activos (login, JWT, SmartTime, BD). Archivos a revisar: Startup, ServicioLogin, AuthService, SmartTimeController, ServicioSmartTimeUsuario. | Riesgos de diseño no identificados. | MEDIO |
| 14 | **Serialización/deserialización insegura** | Uso de BinaryFormatter o deserialización de datos no confiables. | Buscar BinaryFormatter, NetDataContractSerializer, ObjectStateFormatter; revisar que JSON sea el único formato de entrada en API (WebApiConfig, model binding). | Ejecución de código remota (RCE). | BAJO (si solo hay JSON) |
| 15 | **Versionado de API y compatibilidad backward** | Que cambios en contratos no rompan clientes existentes; ruta o header de versión. | Revisar rutas en WebApiConfig (RoutePrefix, MapHttpRoute); si hay v1/v2; revisar Controllers y si se documenta breaking change. Archivos: WebApiConfig.cs, Controllers, SwaggerConfig. | Clientes que dejan de funcionar tras un despliegue. | MEDIO |
| 16 | **Observabilidad (métricas, tracing)** | Métricas de negocio/errores y trazado de requests entre servicios. | Buscar uso de métricas (PerformanceCounter, Application Insights, Prometheus); tracing (correlation id ya en RequestIdHandler). Archivos: Serilog config, Global.asax, Handlers, Web.config. Pruebas: verificar que X-Request-Id se propague en logs. | Dificultad para diagnosticar fallos en producción. | BAJO |
| 17 | **Backups/migraciones y estrategia de rollback** | Que existan backups de BD y procedimiento de rollback ante fallos. | **No verificable** solo con código. Requiere documentación o runbook de despliegue (dónde está el script de backup, cómo se restaura). Pedir: documento de estrategia de backup/rollback o pipeline de release. | Pérdida de datos o indisponibilidad prolongada. | ALTO (proceso) |
| 18 | **Linter / format + convenciones + code review checklist** | Estilo de código uniforme y checklist de revisión antes de merge. | Buscar .editorconfig, StyleCop, reglas de análisis en .csproj; si hay documento de convenciones o checklist en repo (p. ej. docs/CODE_REVIEW.md). Archivos: raíz del repo, .csproj. | Código inconsistente y bugs por convenciones no aplicadas. | BAJO |

**No verificable solo con código/repo:** Backups, migraciones y estrategia de rollback (proceso/infra); que en producción no se desplieguen archivos .txt de credenciales (proceso de despliegue); que appSettings.secrets.config no exista en el repositorio en todos los entornos (verificar en cada clone/pipeline).

---

*Informe generado a partir únicamente del código del workspace. Todo hallazgo está referenciado a archivos y fragmentos concretos. No se inventan problemas sin evidencia en el código.*
