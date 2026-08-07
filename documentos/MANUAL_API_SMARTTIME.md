# Manual de usuario – API SmartTime (SmartLunch)

**Versión:** 1.0  
**Fecha:** Febrero 2025  
**Alcance:** Solo endpoints relacionados con la integración SmartTime.

---

## 1. Introducción

Este manual describe cómo consumir las **API de SmartTime** expuestas por SmartLunch. Están pensadas para:

- **Integración con SmartTime** (sistema de datos laborales): consulta de dato laboral y sincronización de usuarios.
- **Administración de usuarios creados desde SmartTime**: listar, crear, actualizar y dar de baja usuarios cuyo origen es smarTime.

Todas las operaciones requieren **autenticación con token JWT** (usuario `smarTime` o un usuario con rol Admin/Gerencia, según el endpoint).

---

## 2. Autenticación

### 2.1 Obtener el token

Antes de llamar a cualquier endpoint de la API SmartTime, debés autenticarte y obtener un **token JWT**.

**Endpoint de login (público):**

```http
POST /api/login/Autentificar
Content-Type: application/json
```

**Cuerpo de la solicitud (ejemplo para usuario SmartTime):**

```json
{
  "Username": "smarTime",
  "Password": "tu_contraseña_smartime"
}
```

**Respuesta exitosa (200):**

```json
{
  "Token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "Usuario": { ... },
  "Login": { ... }
}
```

- El valor de **`Token`** es el que debés usar en todas las llamadas siguientes.

### 2.2 Enviar el token en las peticiones

En cada solicitud a la API, enviá el token en el **header**:

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

- Sin este header (o con token inválido/expirado), la API responderá **401 No autorizado**.

---

## 3. Base URL y convenciones

- **Base URL:** La URL base de la API la define el entorno (ej. `https://tu-servidor/smartlunch` o `http://localhost:puerto`). Todos los paths que se indican más abajo son relativos a esa base.
- **Formato:** Las peticiones y respuestas usan **JSON** (`Content-Type: application/json`).
- **Códigos HTTP:** Se usan los códigos estándar (200 OK, 201 Created, 400 Bad Request, 401 Unauthorized, 404 Not Found, 500 Internal Server Error, 502 Bad Gateway).

---

## 4. Endpoints de la API SmartTime

A continuación se detallan **solo** los endpoints relacionados con SmartTime.

---

### 4.1 Verificar si existe el usuario SmartTime

Permite saber si el usuario de sistema **smarTime** (el usado para que el sistema se autentique en la API) está creado en la base de datos.

| Método | Ruta | Descripción |
|--------|------|-------------|
| **GET** | `/api/usuario/smarttime/existe` | Indica si existe el usuario smarTime en el sistema |

**Autenticación:** Cualquier usuario autenticado (token válido).

**Parámetros:** Ninguno.

**Respuesta exitosa (200):**

```json
{
  "existe": true
}
```

o

```json
{
  "existe": false
}
```

- **`existe`**: `true` si existe un login con username `smarTime` activo; `false` en caso contrario (o si hay error de base de datos).

**Uso típico:** El front o un proceso de integración puede llamar a este endpoint para decidir si debe mostrar configuración inicial o si ya puede usar el usuario smarTime para operar.

---

### 4.2 Obtener dato laboral por legajo

Consulta el **dato laboral** de un legajo. La API actúa como proxy hacia el sistema SmartTime externo (configuración en el servidor).

| Método | Ruta | Descripción |
|--------|------|-------------|
| **GET** | `/api/smartime/dato-laboral/{legajo}` | Obtiene el dato laboral del legajo desde SmartTime |

**Autenticación:** Cualquier usuario autenticado.

**Parámetros de ruta:**

| Parámetro | Tipo   | Obligatorio | Descripción        |
|-----------|--------|-------------|--------------------|
| `legajo`  | entero | Sí          | Número de legajo   |

**Ejemplo de solicitud:**

```http
GET /api/smartime/dato-laboral/12345
Authorization: Bearer {token}
```

**Respuesta exitosa (200):**

El cuerpo es el que devuelve el servicio SmartTime externo, deserializado. Estructura típica (puede variar según configuración):

```json
{
  "datoLaboral": [
    {
      "legajo": 12345,
      "apellidoNombre": "Apellido, Nombre",
      "dni": 12345678,
      "cuil": "20-12345678-9"
    }
  ]
}
```

- Si no hay datos para el legajo, `datoLaboral` puede ser un array vacío.

**Errores frecuentes:**

- **502 Bad Gateway:** Error al comunicarse con SmartTime (servicio externo no disponible o error de red). El cuerpo incluye `message` y `detail`.

---

### 4.3 Crear usuario desde SmartTime

Crea un **usuario** en SmartLunch con origen **smarTime** (sl_usuario + sl_login). Se usan valores por defecto de catálogo y jerarquía Gerencia. El **username** del login será el **DNI**; la contraseña inicial es la configurada por defecto para usuarios SmartTime y el usuario deberá cambiarla en el primer acceso.

| Método | Ruta | Descripción |
|--------|------|-------------|
| **POST** | `/api/smartime/usuarios` | Crea un usuario desde la integración SmartTime |

**Autenticación:** Solo roles **Admin** o **Gerencia**.

**Cuerpo de la solicitud (JSON):**

| Campo         | Tipo    | Obligatorio | Descripción |
|---------------|---------|-------------|-------------|
| `Nombre`      | string  | Sí          | Máx. 50 caracteres |
| `Apellido`    | string  | Sí          | Máx. 50 caracteres |
| `Legajo`      | entero  | Sí          | Mayor a 0 |
| `Dni`         | entero  | Sí          | Entre 1 y 99.999.999 |
| `Cuil`        | string  | Sí          | CUIL válido (formato 20-12345678-9) |
| `Domicilio`   | string  | No          | Máx. 100 caracteres |
| `FechaIngreso`| string (fecha) | No  | Fecha de ingreso (ISO o compatible) |

**Ejemplo de cuerpo:**

```json
{
  "Nombre": "Juan",
  "Apellido": "Pérez",
  "Legajo": 1001,
  "Dni": 30111222,
  "Cuil": "20-30111222-9",
  "Domicilio": "Calle Falsa 123",
  "FechaIngreso": "2024-01-15"
}
```

**Respuesta exitosa (201 Created):**

```json
{
  "Id": 42,
  "Legajo": 1001,
  "Nombre": "Juan",
  "Apellido": "Pérez",
  "Username": "30111222",
  "RequiereCambioClave": true
}
```

- **Username:** Es el DNI del usuario (será el nombre de usuario para iniciar sesión).
- **RequiereCambioClave:** Indica que debe cambiar la contraseña en el primer acceso.

**Errores frecuentes (400 Bad Request):**

- Datos faltantes o inválidos (nombre, apellido, legajo, DNI, CUIL).
- CUIL con formato inválido.
- Ya existe un usuario con el mismo **DNI**, **legajo** o **CUIL**.
- Ya existe un login con ese DNI como username (el DNI es único para acceso).

---

### 4.4 Listar usuarios SmartTime

Devuelve un **listado paginado** de usuarios creados por smarTime (origen_datos o createuser = "smarTime"). Por defecto solo se listan usuarios **activos** (no dados de baja).

| Método | Ruta | Descripción |
|--------|------|-------------|
| **GET** | `/api/smartime/usuarios` | Lista usuarios de origen SmartTime (paginado) |

**Autenticación:** Solo roles **Admin** o **Gerencia**.

**Parámetros de consulta (query):**

| Parámetro     | Tipo   | Por defecto | Descripción |
|---------------|--------|-------------|-------------|
| `page`        | entero | 1           | Número de página |
| `pageSize`    | entero | 10          | Cantidad por página (máx. 100) |
| `search`      | string | null        | Buscar en nombre, apellido, legajo, DNI, CUIL |
| `soloActivos` | bool   | true        | `true` = solo activos; `false` = solo inactivos (dados de baja) |

**Ejemplo de solicitud:**

```http
GET /api/smartime/usuarios?page=1&pageSize=10&search=Juan&soloActivos=true
Authorization: Bearer {token}
```

**Respuesta exitosa (200):**

```json
{
  "page": 1,
  "pageSize": 10,
  "totalItems": 25,
  "totalPages": 3,
  "items": [
    {
      "Id": 42,
      "Legajo": 1001,
      "Nombre": "Juan",
      "Apellido": "Pérez",
      "Dni": 30111222,
      "Cuil": "20-30111222-9",
      "Domicilio": "Calle Falsa 123",
      "FechaIngreso": "2024-01-15T00:00:00",
      "Username": "30111222",
      "Activo": true
    }
  ]
}
```

---

### 4.5 Actualizar usuario SmartTime por legajo

Actualiza los datos de un usuario que fue **creado por smarTime**. Solo se pueden editar usuarios con origen smarTime. El legajo va en la URL.

| Método | Ruta | Descripción |
|--------|------|-------------|
| **PUT** | `/api/smartime/usuarios/{legajo}` | Actualiza un usuario SmartTime por legajo |

**Autenticación:** Solo rol **Admin**.

**Parámetros de ruta:**

| Parámetro | Tipo   | Obligatorio | Descripción |
|-----------|--------|-------------|-------------|
| `legajo`  | entero | Sí          | Legajo del usuario a actualizar |

**Cuerpo de la solicitud (JSON):**

| Campo          | Tipo   | Obligatorio | Descripción |
|----------------|--------|-------------|-------------|
| `Nombre`       | string | Sí          | Máx. 50 caracteres |
| `Apellido`     | string | Sí          | Máx. 50 caracteres |
| `Dni`          | entero | Sí          | Entre 1 y 99.999.999 |
| `Cuil`         | string | Sí          | CUIL válido |
| `Domicilio`    | string | No          | Máx. 100 caracteres |
| `FechaIngreso` | string (fecha) | No  | Fecha de ingreso |

**Ejemplo de solicitud:**

```http
PUT /api/smartime/usuarios/1001
Content-Type: application/json
Authorization: Bearer {token}

{
  "Nombre": "Juan Carlos",
  "Apellido": "Pérez",
  "Dni": 30111222,
  "Cuil": "20-30111222-9",
  "Domicilio": "Nueva dirección 456",
  "FechaIngreso": "2024-01-15"
}
```

**Respuesta exitosa (200):**

```json
{
  "message": "Usuario actualizado correctamente."
}
```

**Errores frecuentes (400 Bad Request):**

- Usuario no encontrado para ese legajo.
- El usuario no es de origen smarTime (no se puede editar por esta API).
- DNI o CUIL duplicados con otro usuario.
- CUIL inválido.

---

### 4.6 Dar de baja usuario SmartTime por legajo

Da de baja lógica (marca `deletemark`) a un usuario creado por smarTime y a sus logins asociados. El usuario deja de aparecer en el listado cuando se filtra por activos.

| Método | Ruta | Descripción |
|--------|------|-------------|
| **DELETE** | `/api/smartime/usuarios/{legajo}` | Da de baja un usuario SmartTime por legajo |

**Autenticación:** Solo roles **Admin** o **Gerencia**.

**Parámetros de ruta:**

| Parámetro | Tipo   | Obligatorio | Descripción |
|-----------|--------|-------------|-------------|
| `legajo`  | entero | Sí          | Legajo del usuario a dar de baja |

**Ejemplo de solicitud:**

```http
DELETE /api/smartime/usuarios/1001
Authorization: Bearer {token}
```

**Respuesta exitosa (200):**

```json
{
  "message": "Usuario dado de baja correctamente."
}
```

**Errores frecuentes (400 Bad Request):**

- Usuario no encontrado para ese legajo.
- El usuario no es de origen smarTime (no se puede dar de baja por esta API).

---

## 5. Resumen de permisos por endpoint

| Endpoint | Método | Roles permitidos |
|----------|--------|-------------------|
| `/api/usuario/smarttime/existe` | GET | Cualquier usuario autenticado |
| `/api/smartime/dato-laboral/{legajo}` | GET | Cualquier usuario autenticado |
| `/api/smartime/usuarios` | POST | Admin, Gerencia |
| `/api/smartime/usuarios` | GET | Admin, Gerencia |
| `/api/smartime/usuarios/{legajo}` | PUT | Admin |
| `/api/smartime/usuarios/{legajo}` | DELETE | Admin, Gerencia |

---

## 6. Códigos de respuesta y errores

- **200 OK:** Operación correcta (GET, PUT, DELETE).
- **201 Created:** Recurso creado (POST crear usuario).
- **400 Bad Request:** Datos inválidos, validación fallida o regla de negocio (ej. DNI/CUIL duplicado, usuario no smarTime).
- **401 Unauthorized:** Sin token o token inválido/expirado. Incluir header `Authorization: Bearer {token}`.
- **404 Not Found:** Recurso no encontrado (no usado en los endpoints documentados; en su lugar suele devolverse 400 con mensaje).
- **500 Internal Server Error:** Error interno del servidor. El cuerpo puede incluir `message` y `detail`.
- **502 Bad Gateway:** Error al comunicarse con el servicio SmartTime externo (p. ej. dato laboral).

### 6.1 Formato de los errores (solo API SmartTime)

Todas las respuestas de error son **JSON** (`Content-Type: application/json`). El código HTTP viene en el **status** de la respuesta. Según el caso, el cuerpo tiene uno de estos formatos:

**401 Unauthorized** (sin token, token inválido o expirado):

```json
{
  "Message": "Token de autenticación expirado o inválido. Por favor, inicie sesión nuevamente.",
  "error": "Token de autenticación expirado o inválido. Por favor, inicie sesión nuevamente."
}
```

**400 Bad Request** (validación o regla de negocio en `api/smartime/*`):

- Mensaje en texto plano en el cuerpo, o objeto con mensaje según el framework. Ejemplo típico de mensaje: *"Ya existe un usuario con el mismo DNI."*

**400 Bad Request** (solo `GET /api/usuario/smarttime/existe` si falla):

```json
{
  "error": "Texto del error"
}
```

**502 Bad Gateway** (solo `GET /api/smartime/dato-laboral/{legajo}` cuando falla SmartTime externo):

```json
{
  "message": "Error consultando SmartTime DatoLaboral",
  "detail": "Mensaje técnico de la excepción"
}
```

**500 Internal Server Error** (errores en POST/PUT/DELETE o GET listar usuarios en `api/smartime/*`):

```json
{
  "message": "Error al crear usuario desde smarTime",
  "detail": "Mensaje técnico de la excepción"
}
```

- En listar: `"message": "Error al listar usuarios smarTime"`.
- En actualizar: `"message": "Error al actualizar usuario smarTime"`.
- En dar de baja: `"message": "Error al dar de baja usuario smarTime"`.

**500 para `GET /api/usuario/smarttime/existe`:**

```json
{
  "error": "Error al verificar usuario SmartTime: ..."
}
```

**Resumen para el cliente:** Revisar siempre el **código HTTP** (401, 400, 500, 502). En el cuerpo, usar `message` o `error` (o ambos) para mostrar el mensaje al usuario; `detail` es opcional y suele ser más técnico.

---

## 7. Notas importantes para el integrador

1. **Usuario smarTime:** El usuario de sistema `smarTime` se usa para que procesos o integraciones se autentiquen en la API. Su existencia se puede verificar con `GET /api/usuario/smarttime/existe`.
2. **Username = DNI:** Los usuarios creados desde SmartTime tienen como **username** de login su **DNI** (numérico). El DNI debe ser único en el sistema.
3. **Contraseña inicial:** Al crear un usuario por `POST /api/smartime/usuarios`, se asigna una contraseña por defecto y `RequiereCambioClave: true`; el usuario debe cambiarla en el primer acceso.
4. **CUIL:** Debe tener formato válido (ej. 20-12345678-9). La API valida el formato antes de crear o actualizar.
5. **Origen smarTime:** Solo los usuarios creados por esta integración (origen_datos/createuser = "smarTime") pueden ser actualizados o dados de baja por los endpoints PUT y DELETE de `/api/smartime/usuarios/{legajo}`.
6. **Dato laboral:** `GET /api/smartime/dato-laboral/{legajo}` depende de la configuración del servidor (SmartTime externo). Si el servicio externo no está disponible, se devolverá 502.

---

## 8. Ejemplo de flujo típico

1. **Login** con usuario `smarTime` (o Admin/Gerencia):  
   `POST /api/login/Autentificar` con `Username` y `Password` → obtener `Token`.

2. **Verificar** si el usuario SmartTime existe (opcional):  
   `GET /api/usuario/smarttime/existe` → `existe: true/false`.

3. **Consultar dato laboral** de un legajo:  
   `GET /api/smartime/dato-laboral/12345`.

4. **Crear usuario** desde SmartTime (Admin/Gerencia):  
   `POST /api/smartime/usuarios` con cuerpo JSON.

5. **Listar usuarios** SmartTime:  
   `GET /api/smartime/usuarios?page=1&pageSize=10`.

6. **Actualizar** (Admin):  
   `PUT /api/smartime/usuarios/1001` con cuerpo JSON.

7. **Dar de baja** (Admin/Gerencia):  
   `DELETE /api/smartime/usuarios/1001`.

En todas las llamadas de los pasos 2 a 7 se debe enviar el header:  
`Authorization: Bearer {Token_obtenido_en_paso_1}`.

---

*Fin del manual – API SmartTime.*
