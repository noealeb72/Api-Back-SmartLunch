# Manual de API - SmartLunch Secure API

**Versión:** 1.0  
**Fecha:** Enero 2025  
**Autor:** Equipo de Desarrollo SmartLunch

---

## Tabla de Contenidos

1. [Introducción](#introducción)
2. [Autenticación](#autenticación)
3. [Estructura de Respuestas](#estructura-de-respuestas)
4. [Códigos de Estado HTTP](#códigos-de-estado-http)
5. [Endpoints por Módulo](#endpoints-por-módulo)
   - [Autenticación](#autenticación-1)
   - [Inicio/Dashboard](#iniciodashboard)
   - [Comandas](#comandas)
   - [Menú del Día](#menú-del-día)
   - [Platos](#platos)
   - [Usuarios](#usuarios)
   - [Reportes](#reportes)
   - [Catálogos](#catálogos)
6. [Modelos de Datos](#modelos-de-datos)
7. [Ejemplos de Uso](#ejemplos-de-uso)
8. [Glosario](#glosario)

---

## Introducción

### Propósito del Documento

Este manual proporciona documentación completa de la API REST de SmartLunch, incluyendo todos los endpoints disponibles, sus parámetros, respuestas y ejemplos de uso.

### Base URL

```
http://localhost:8000/api
```

### Formato de Datos

- **Content-Type:** `application/json`
- **Encoding:** UTF-8
- **Formato de Fecha:** `YYYY-MM-DD` o `YYYY-MM-DDTHH:mm:ss`

### CORS

La API permite solicitudes desde cualquier origen (`*`). Todos los endpoints (excepto login) requieren autenticación mediante JWT.

---

## Autenticación

### Método de Autenticación

La API utiliza **JSON Web Tokens (JWT)** para autenticación. Después de autenticarse exitosamente, el token debe incluirse en todas las solicitudes subsiguientes.

### Header de Autenticación

```
Authorization: Bearer {token}
```

### Obtener Token

Ver sección [POST /api/login/Autentificar](#post-apiloginautentificar)

---

## Estructura de Respuestas

### Respuesta Exitosa

```json
{
  "ok": true,
  "data": { ... },
  "message": "Operación exitosa"
}
```

### Respuesta de Error

```json
{
  "ok": false,
  "message": "Descripción del error",
  "errors": [ ... ]
}
```

### Respuesta Paginada

```json
{
  "ok": true,
  "data": {
    "page": 1,
    "pageSize": 10,
    "totalItems": 100,
    "totalPages": 10,
    "items": [ ... ]
  }
}
```

---

## Códigos de Estado HTTP

| Código | Descripción | Uso |
|--------|-------------|-----|
| 200 | OK | Solicitud exitosa |
| 201 | Created | Recurso creado exitosamente |
| 400 | Bad Request | Datos inválidos o parámetros incorrectos |
| 401 | Unauthorized | Token inválido o ausente |
| 403 | Forbidden | Sin permisos para acceder al recurso |
| 404 | Not Found | Recurso no encontrado |
| 429 | Too Many Requests | Demasiados intentos (rate limiting) |
| 500 | Internal Server Error | Error interno del servidor |

---

## Endpoints por Módulo

---

## Autenticación

### POST /api/login/Autentificar

Autentica un usuario y retorna un token JWT.

**Autenticación:** No requerida (AllowAnonymous)

**Parámetros (Body):**

```json
{
  "username": "string (requerido)",
  "password": "string (requerido)"
}
```

**Respuesta Exitosa (200):**

```json
{
  "ok": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "usuario": {
      "id": 1,
      "nombre": "Juan",
      "apellido": "Pérez",
      "legajo": 12345,
      "email": "juan.perez@example.com",
      "plantaId": 1,
      "plantaNombre": "Planta Central",
      "centroCostoId": 1,
      "centroCostoNombre": "CC001",
      "proyectoId": 1,
      "proyectoNombre": "Proyecto A",
      "jerarquiaId": 1,
      "jerarquiaNombre": "Gerencia",
      "planNutricionalId": 1,
      "planNutricionalNombre": "Plan Estándar",
      "activo": true
    }
  }
}
```

**Errores Posibles:**
- `400`: Datos inválidos
- `401`: Credenciales inválidas
- `429`: Demasiados intentos fallidos (rate limiting)

**Ejemplo de Uso:**

```bash
curl -X POST http://localhost:8000/api/login/Autentificar \
  -H "Content-Type: application/json" \
  -d '{
    "username": "usuario",
    "password": "contraseña"
  }'
```

---

## Inicio/Dashboard

### GET /api/inicio/web

Obtiene los datos iniciales para el dashboard web: usuario, turnos disponibles, menú del día del primer turno y comandas del día.

**Autenticación:** Requerida

**Parámetros:** Ninguno (el usuarioId se obtiene del token)

**Respuesta Exitosa (200):**

```json
{
  "ok": true,
  "data": {
    "usuario": {
      "id": 1,
      "nombre": "Juan",
      "apellido": "Pérez",
      "legajo": 12345,
      "dni": 12345678,
      "foto": "url_foto.jpg",
      "plantaId": 1,
      "plantaNombre": "Planta Central",
      "centroCostoId": 1,
      "centroCostoNombre": "CC001",
      "proyectoId": 1,
      "proyectoNombre": "Proyecto A",
      "jerarquiaId": 1,
      "jerarquiaNombre": "Gerencia",
      "planNutricionalId": 1,
      "planNutricionalNombre": "Plan Estándar",
      "bonificaciones": 10,
      "bonificacionesInvitado": 5,
      "pedidos": 50,
      "activo": true
    },
    "turnos": [
      {
        "id": 1,
        "nombre": "Desayuno",
        "horaInicio": "07:00",
        "horaFin": "10:00"
      },
      {
        "id": 2,
        "nombre": "Almuerzo",
        "horaInicio": "12:00",
        "horaFin": "15:00"
      }
    ],
    "menuDelDia": [
      {
        "id": 1,
        "fecha": "2025-01-15",
        "turnoId": 1,
        "turnoNombre": "Desayuno",
        "platoId": 10,
        "platoNombre": "Café con Leche",
        "cantidad": 50,
        "disponible": 45,
        "despachado": 5,
        "plantaId": 1,
        "plantaNombre": "Planta Central",
        "centroCostoId": 1,
        "centroCostoNombre": "CC001",
        "proyectoId": 1,
        "proyectoNombre": "Proyecto A",
        "jerarquiaId": 1,
        "jerarquiaNombre": "Gerencia",
        "foto": "url_foto.jpg",
        "nutricionalId": 1,
        "nutricionalNombre": "Plan Estándar",
        "importe": 150.00,
        "estado": "DISPONIBLE"
      }
    ],
    "platosPedidos": [
      {
        "id": 1,
        "npedido": 12345,
        "fecha": "2025-01-15T12:00:00",
        "monto": 150.00,
        "estado": "P",
        "platoId": 10,
        "platoNombre": "Café con Leche",
        "turnoId": 1,
        "turnoNombre": "Desayuno",
        "bonificado": false,
        "invitado": false,
        "calificacion": 5,
        "comentario": "Excelente",
        "platoImporte": 150.00,
        "foto": "url_foto.jpg",
        "nutricionalId": 1,
        "nutricionalNombre": "Plan Estándar"
      }
    ]
  }
}
```

**Uso:** Carga inicial del dashboard. Trae el primer turno disponible por defecto.

---

### GET /api/inicio/web-actualizado

Obtiene datos actualizados del dashboard para un turno específico. Ideal para actualización periódica cada 2 segundos.

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `turnoId` | int | Sí | ID del turno seleccionado |
| `fecha` | string | No | Fecha en formato YYYY-MM-DD (por defecto: hoy) |

**Ejemplo de URL:**

```
GET /api/inicio/web-actualizado?turnoId=2&fecha=2025-01-15
```

**Respuesta:** Misma estructura que `/api/inicio/web` pero filtrada por el turno seleccionado.

**Uso:** Actualización periódica del dashboard cuando el usuario cambia de turno.

---

### GET /api/inicio/totem

Obtiene datos de inicio para tótem físico (no requiere autenticación, solo legajo).

**Autenticación:** No requerida (AllowAnonymous)

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `legajo` | int | Sí | Número de legajo del usuario |

**Respuesta:** Similar a `/api/inicio/web` pero sin `platosPedidos`.

---

## Comandas

### GET /api/comanda/lista

Obtiene una lista paginada de comandas con filtros opcionales.

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `page` | int | No | Número de página (default: 1) |
| `pageSize` | int | No | Tamaño de página (default: 10, max: 100) |
| `fechaDesde` | DateTime | No | Fecha desde para filtrar |
| `fechaHasta` | DateTime | No | Fecha hasta para filtrar |
| `usuarioId` | int? | No | Filtrar por ID de usuario |
| `turnoId` | int? | No | Filtrar por ID de turno |
| `plantaId` | int? | No | Filtrar por ID de planta |
| `centroCostoId` | int? | No | Filtrar por ID de centro de costo |
| `proyectoId` | int? | No | Filtrar por ID de proyecto |
| `jerarquiaId` | int? | No | Filtrar por ID de jerarquía |
| `estado` | string | No | Filtrar por estado (P, E, R, D, C) |
| `search` | string | No | Búsqueda por texto (turno, plato, planta, etc.) |

**Respuesta Exitosa (200):**

```json
{
  "ok": true,
  "data": {
    "page": 1,
    "pageSize": 10,
    "totalItems": 100,
    "totalPages": 10,
    "items": [
      {
        "id": 1,
        "npedido": 12345,
        "fecha": "2025-01-15T12:00:00",
        "monto": 150.00,
        "estado": "P",
        "usuarioId": 1,
        "usuarioNombre": "Juan Pérez",
        "platoId": 10,
        "platoDescripcion": "Café con Leche",
        "turnoId": 1,
        "turnoNombre": "Desayuno",
        "plantaId": 1,
        "plantaDescripcion": "Planta Central",
        "centroDeCostoId": 1,
        "centroDeCostoDescripcion": "CC001",
        "proyectoId": 1,
        "proyectoDescripcion": "Proyecto A",
        "jerarquiaId": 1,
        "jerarquiaDescripcion": "Gerencia",
        "bonificado": false,
        "invitado": false,
        "calificacion": 5,
        "foto": "url_foto.jpg"
      }
    ]
  }
}
```

---

### GET /api/comanda/{id}

Obtiene el detalle completo de una comanda por su ID.

**Autenticación:** Requerida

**Parámetros (Path):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `id` | int | Sí | ID de la comanda |

**Respuesta:** Objeto `ComandaDetalleDto` con todos los campos.

---

### POST /api/comanda/crear

Crea una nueva comanda (pedido).

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "menuddId": 1,
  "platoId": 10,
  "turnoId": 1,
  "monto": 150.00,
  "bonificado": false,
  "invitado": false,
  "estado": "P",
  "comentario": "Sin cebolla",
  "plantaId": 1,
  "centroDeCostoId": 1,
  "proyectoId": 1,
  "jerarquiaId": 1
}
```

**Nota:** El `usuarioId` se obtiene del token automáticamente. El `npedido` se genera automáticamente en la base de datos.

**Respuesta Exitosa (201):**

```json
{
  "ok": true,
  "data": {
    "id": 1,
    "npedido": 12345,
    "fecha": "2025-01-15T12:00:00",
    "monto": 150.00,
    "estado": "P",
    ...
  }
}
```

---

### PUT /api/comanda/actualizar

Actualiza una comanda existente.

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "id": 1,
  "monto": 200.00,
  "bonificado": false,
  "invitado": false,
  "calificacion": 5,
  "estado": "P",
  "comentario": "Actualizado"
}
```

---

### POST /api/comanda/eliminar

Elimina lógicamente una comanda (soft delete).

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "npedido": 12345
}
```

---

### POST /api/comanda/activar

Reactiva una comanda eliminada lógicamente.

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "npedido": 12345
}
```

---

### PUT /api/comanda/cancelar

Cancela una comanda pendiente (cambia estado a "C" - Cancelado).

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "npedido": 12345
}
```

**Estados Válidos:** Solo comandas en estado "P" (Pendiente) pueden ser canceladas.

---

### PUT /api/comanda/{npedido}/despachar

Despacha una comanda (cambia estado a "E" - En Aceptación).

**Autenticación:** Requerida

**Parámetros (Path):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `npedido` | int | Sí | Número de pedido |

**Parámetros (Body):**

```json
{
  "npedido": 12345
}
```

**Estados Válidos:** Solo comandas en estado "P" (Pendiente) pueden ser despachadas.

**Efectos:** Incrementa el contador `despachado` en el menú del día.

---

### PUT /api/comanda/{npedido}/recibir

Recibe una comanda (cambia estado a "R" - Recibido). Permite guardar la calificación del usuario.

**Autenticación:** Requerida

**Parámetros (Path):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `npedido` | int | Sí | Número de pedido |

**Parámetros (Body):**

```json
{
  "npedido": 12345,
  "calificacion": 5
}
```

| Campo | Tipo | Requerido | Descripción |
|-------|------|-----------|-------------|
| `npedido` | int | Sí | Número de pedido |
| `calificacion` | int? | No | Calificación del usuario (típicamente 1-5) |

**Estados Válidos:** Solo comandas en estado "E" (En Aceptación) pueden ser recibidas.

**Efectos:** Guarda la calificación en la base de datos si se proporciona.

---

### PUT /api/comanda/{npedido}/devolver

Devuelve una comanda (cambia estado a "D" - Devuelto). Permite guardar la calificación del usuario.

**Autenticación:** Requerida

**Parámetros (Path):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `npedido` | int | Sí | Número de pedido |

**Parámetros (Body):**

```json
{
  "npedido": 12345,
  "calificacion": 3
}
```

| Campo | Tipo | Requerido | Descripción |
|-------|------|-----------|-------------|
| `npedido` | int | Sí | Número de pedido |
| `calificacion` | int? | No | Calificación del usuario (típicamente 1-5) |

**Estados Válidos:** Solo comandas en estado "E" (En Aceptación) o "R" (Recibido) pueden ser devueltas.

**Efectos:** 
- Guarda la calificación en la base de datos si se proporciona.
- Decrementa el contador `comandas` en el menú del día si corresponde.

---

## Menú del Día

### GET /api/menudd/lista

Obtiene una lista paginada de menús del día con filtros y búsqueda.

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `page` | int | No | Número de página (default: 1) |
| `pageSize` | int | No | Tamaño de página (default: 10) |
| `fechaDesde` | DateTime | No | Fecha desde para filtrar |
| `fechaHasta` | DateTime | No | Fecha hasta para filtrar |
| `search` | string | No | Búsqueda por: turno, plato, planta, centro de costo, proyecto, jerarquía |
| `activo` | bool | No | Solo activos (default: true) |

**Respuesta:** Lista paginada de `MenuddListadoDto`.

---

### GET /api/menudd/{id}

Obtiene el detalle completo de un menú del día por su ID.

**Autenticación:** Requerida

**Parámetros (Path):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `id` | int | Sí | ID del menú del día |

**Uso:** Para cargar datos en formulario de edición.

---

### POST /api/menudd/crear

Crea un nuevo menú del día.

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "fecha": "2025-01-15",
  "turnoId": 1,
  "platoId": 10,
  "cantidad": 50,
  "plantaId": 1,
  "centroDeCostoId": 1,
  "proyectoId": 1,
  "jerarquiaId": 1
}
```

**Campos Requeridos:**
- `fecha`: Fecha del menú
- `turnoId`: ID del turno
- `platoId`: ID del plato
- `cantidad`: Cantidad disponible
- `plantaId`: ID de la planta
- `centroDeCostoId`: ID del centro de costo
- `proyectoId`: ID del proyecto
- `jerarquiaId`: ID de la jerarquía (requerido)

---

### PUT /api/menudd/actualizar

Actualiza un menú del día existente.

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "id": 1,
  "fecha": "2025-01-15",
  "turnoId": 1,
  "platoId": 10,
  "cantidad": 60,
  "plantaId": 1,
  "centroDeCostoId": 1,
  "proyectoId": 1,
  "jerarquiaId": 1
}
```

---

### POST /api/menudd/baja

Elimina lógicamente un menú del día.

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `id` | int | Sí | ID del menú del día |

---

### GET /api/menudd/por-turno

Obtiene el menú del día y comandas para un turno específico.

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `fecha` | DateTime | Sí | Fecha del menú |
| `plantaId` | int | Sí | ID de la planta |
| `turnoId` | int | Sí | ID del turno |
| `centroCostoId` | int? | No | ID del centro de costo |
| `proyectoId` | int? | No | ID del proyecto |
| `jerarquiaId` | int? | No | ID de la jerarquía |
| `nutricionalId` | int? | No | ID del plan nutricional |
| `soloConStock` | bool | No | Solo con stock disponible (default: true) |

**Respuesta:**

```json
{
  "ok": true,
  "data": {
    "menuDelDia": [ ... ],
    "comandas": [ ... ]
  }
}
```

---

### POST /api/menudd/impresion

Obtiene datos para impresión del menú del día (sin paginación, todos los registros según filtros).

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "fechaDesde": "2025-01-01",
  "fechaHasta": "2025-01-31",
  "search": "texto",
  "columnas": [
    "fecha",
    "turnoNombre",
    "platoNombre",
    "cantidad",
    "disponible"
  ]
}
```

**Uso:** Para generar reportes PDF/Excel con todos los datos filtrados.

---

## Platos

### GET /api/plato/lista

Obtiene una lista paginada de platos.

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `page` | int | No | Número de página (default: 1) |
| `pageSize` | int | No | Tamaño de página (default: 10) |
| `search` | string | No | Búsqueda por código o descripción |
| `activo` | bool | No | Solo activos (default: true) |

---

### GET /api/plato/{id}

Obtiene el detalle completo de un plato por su ID.

**Autenticación:** Requerida

---

### GET /api/plato/buscar

Buscador de platos para autocomplete (devuelve todos los campos).

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `texto` | string | No | Texto a buscar |
| `soloActivos` | bool | No | Solo activos (default: true) |
| `maxResultados` | int | No | Máximo de resultados (default: 20) |

---

### GET /api/plato/buscar-simple

Buscador simple que devuelve solo código y nombre.

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `texto` | string | No | Texto a buscar |
| `soloActivos` | bool | No | Solo activos (default: true) |
| `maxResultados` | int | No | Máximo de resultados (default: 20) |

---

### POST /api/plato/crear

Crea un nuevo plato.

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "codigo": "PLATO001",
  "descripcion": "Café con Leche",
  "costo": 150.00,
  "costoProveedor": 90.00,
  "foto": "url_foto.jpg",
  "planNutricionalId": 1,
  "activo": true
}
```

`costo` es el precio de lista que ve el empleado antes de aplicar la bonificación de su jerarquía. `costoProveedor` es lo que realmente factura el proveedor por ese plato (para conciliación de facturas); se guarda por separado y se congela en cada comanda al momento del pedido, así los reportes históricos no cambian si el costo del plato se actualiza después.

---

### PUT /api/plato/actualizar

Actualiza un plato existente. Usa el mismo body que `POST /api/plato/crear` (incluye `costo` y `costoProveedor`), agregando el campo `id`.

**Autenticación:** Requerida

---

### POST /api/plato/eliminar

Elimina lógicamente un plato.

**Autenticación:** Requerida

---

### POST /api/plato/impresion

Obtiene datos para impresión de platos (sin paginación).

**Autenticación:** Requerida

**Parámetros (Body):**

```json
{
  "search": "texto",
  "activo": true,
  "columnas": [
    "codigo",
    "descripcion",
    "costo",
    "planNutricionalNombre"
  ]
}
```

---

## Usuarios

### GET /api/usuario/lista

Obtiene una lista paginada de usuarios con filtros.

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `page` | int | No | Número de página (default: 1) |
| `pageSize` | int | No | Tamaño de página (default: 10) |
| `search` | string | No | Búsqueda por texto |
| `plantaId` | int? | No | Filtrar por ID de planta |
| `centroCostoId` | int? | No | Filtrar por ID de centro de costo |
| `proyectoId` | int? | No | Filtrar por ID de proyecto |
| `jerarquiaId` | int? | No | Filtrar por ID de jerarquía |
| `planNutricionalId` | int? | No | Filtrar por ID de plan nutricional |
| `activo` | bool | No | Solo activos (default: true) |

---

### GET /api/usuario/{id}

Obtiene el detalle completo de un usuario por su ID.

**Autenticación:** Requerida

---

### GET /api/usuario/buscar-simple

Buscador simple de usuarios (devuelve solo legajo y nombre).

**Autenticación:** Requerida

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `texto` | string | No | Texto a buscar (mínimo 4 caracteres) |
| `soloActivos` | bool | No | Solo activos (default: true) |
| `maxResultados` | int | No | Máximo de resultados (default: 20, max: 100) |

---

### POST /api/usuario/crear

Crea un nuevo usuario.

**Autenticación:** Requerida

---

### PUT /api/usuario/actualizar

Actualiza un usuario existente.

**Autenticación:** Requerida

---

### POST /api/usuario/eliminar

Elimina lógicamente un usuario.

**Autenticación:** Requerida

---

## Reportes

### GET /api/reporte/User

Obtiene un reporte detallado de un usuario por legajo.

**Autenticación:** No requerida (AllowAnonymous)

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `user` | string | Sí | Legajo del usuario (numérico) |
| `desde` | DateTime | Sí | Fecha desde |
| `hasta` | DateTime | Sí | Fecha hasta |
| `plantaId` | int? | No | Filtrar por ID de planta |

**Respuesta:**

```json
{
  "ok": true,
  "data": {
    "id": 1,
    "nombre": "Juan",
    "apellido": "Pérez",
    "legajo": 12345,
    "dni": 12345678,
    "foto": "url_foto.jpg",
    "plantaNombre": "Planta Central",
    "planNutricionalNombre": "Plan Estándar",
    "centroDeCostoNombre": "CC001",
    "proyectoNombre": "Proyecto A",
    "jerarquiaNombre": "Gerencia",
    "bonificaciones": 10,
    "bonificacionesInvitadoAcum": 5,
    "bonificadosInvitadosRango": 2,
    "consumidos": [
      {
        "id": 1,
        "npedido": 12345,
        "fecha": "2025-01-15T12:00:00",
        "monto": 150.00,
        "estado": "R",
        "platoId": 10,
        "descripcionPlato": "Café con Leche",
        "turnoId": 1,
        "turnoNombre": "Desayuno",
        "bonificado": false,
        "invitado": false,
        "plato": "Café con Leche"
      }
    ],
    "monto": 1500.00,
    "estados": ["P", "E", "R"],
    "ultimoEstado": "R",
    "ultimoPlato": "Café con Leche",
    "descripcionesPlatos": ["Café con Leche", "Tostado"]
  }
}
```

---

### GET /api/Reporte/Facturacion

Reporte de facturación: comandas recibidas (estado "R") en el rango de fechas, con el reparto empleado/empresa según la bonificación de jerarquía del usuario, y el costo real de proveedor congelado al momento del pedido (para conciliar con la factura del proveedor).

**Autenticación:** Requerida (rol "Gerencia")

**Parámetros (Query):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `fechaDesde` | string | Sí | Fecha desde, formato `YYYY-MM-DD` |
| `fechaHasta` | string | Sí | Fecha hasta, formato `YYYY-MM-DD` |
| `plantaId` | int? | No | Filtrar por ID de planta |
| `proyectoId` | int? | No | Filtrar por ID de proyecto |
| `centrodecostoId` | int? | No | Filtrar por ID de centro de costo |

**Respuesta:**

```json
{
  "ok": true,
  "data": [
    {
      "legajo": "12345",
      "apellido": "Pérez",
      "nombre": "Juan",
      "estadoUsuario": "Activo",
      "fecha": "2025-01-15T12:00:00",
      "platoImporte": 150.00,
      "montoEmpleado": 50.00,
      "montoEmpresa": 100.00,
      "costoProveedor": 90.00,
      "bonificado": true
    }
  ]
}
```

`platoImporte` es el precio de lista del plato; `montoEmpleado`/`montoEmpresa` son el reparto de ese precio según la bonificación de jerarquía; `costoProveedor` es lo que realmente le costó el plato a la empresa (independiente del reparto anterior), congelado al momento del pedido.

---

## Catálogos

### GET /api/turno/combo

Obtiene lista de turnos para combo box.

**Autenticación:** Requerida

---

### GET /api/planta/combo

Obtiene lista de plantas para combo box.

**Autenticación:** Requerida

---

### GET /api/centrodecosto/combo

Obtiene lista de centros de costo para combo box.

**Autenticación:** Requerida

---

### GET /api/proyecto/combo

Obtiene lista de proyectos para combo box.

**Autenticación:** Requerida

---

### GET /api/jerarquia/combo

Obtiene lista de jerarquías para combo box.

**Autenticación:** Requerida

---

### GET /api/plannutricional/combo

Obtiene lista de planes nutricionales para combo box.

**Autenticación:** Requerida

---

## Modelos de Datos

### Estados de Comanda

| Código | Descripción | Significado |
|--------|-------------|-------------|
| `P` | Pendiente | Comanda creada, esperando ser despachada |
| `E` | En Aceptación | Comanda despachada, en proceso de entrega |
| `R` | Recibido | Comanda recibida por el usuario |
| `D` | Devuelto | Comanda devuelta |
| `C` | Cancelado | Comanda cancelada |

### Flujo de Estados

```
P (Pendiente) 
  → E (En Aceptación) [Despachar]
    → R (Recibido) [Recibir]
    → D (Devuelto) [Devolver]
  → C (Cancelado) [Cancelar]
```

---

## Ejemplos de Uso

### Ejemplo 1: Autenticación y Obtención de Datos Iniciales

```javascript
// 1. Autenticarse
const loginResponse = await fetch('http://localhost:8000/api/login/Autentificar', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    username: 'usuario',
    password: 'contraseña'
  })
});

const loginData = await loginResponse.json();
const token = loginData.data.token;

// 2. Obtener datos iniciales del dashboard
const inicioResponse = await fetch('http://localhost:8000/api/inicio/web', {
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  }
});

const inicioData = await inicioResponse.json();
console.log('Usuario:', inicioData.data.usuario);
console.log('Turnos:', inicioData.data.turnos);
console.log('Menú del día:', inicioData.data.menuDelDia);
```

### Ejemplo 2: Crear una Comanda

```javascript
const crearComandaResponse = await fetch('http://localhost:8000/api/comanda/crear?usuarioId=28', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    menuddId: 1,
    platoId: 10,
    turnoId: 1,
    monto: 150.00,
    bonificado: false,
    invitado: false,
    estado: 'P',
    comentario: 'Sin cebolla',
    plantaId: 1,
    centroDeCostoId: 1,
    proyectoId: 1,
    jerarquiaId: 1
  })
});

const comandaCreada = await crearComandaResponse.json();
console.log('Comanda creada:', comandaCreada.data);
```

### Ejemplo 3: Actualización Periódica del Dashboard

```javascript
// Actualizar cada 2 segundos con el turno seleccionado
const turnoSeleccionadoId = 2;

setInterval(async () => {
  const response = await fetch(
    `http://localhost:8000/api/inicio/web-actualizado?turnoId=${turnoSeleccionadoId}`,
    {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      }
    }
  );
  
  const data = await response.json();
  // Actualizar UI con data.data.menuDelDia y data.data.platosPedidos
}, 2000);
```

### Ejemplo 4: Recibir Comanda con Calificación

```javascript
const recibirResponse = await fetch('http://localhost:8000/api/comanda/12345/recibir', {
  method: 'PUT',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    npedido: 12345,
    calificacion: 5  // Calificación de 1 a 5
  })
});

const resultado = await recibirResponse.json();
console.log('Comanda recibida:', resultado);
```

---

## Glosario

### Términos Técnicos

- **JWT (JSON Web Token):** Token de autenticación estándar usado para autorizar solicitudes.
- **CORS (Cross-Origin Resource Sharing):** Permite que aplicaciones web accedan a recursos de otro dominio.
- **Soft Delete:** Eliminación lógica (marca el registro como eliminado sin borrarlo físicamente).
- **DTO (Data Transfer Object):** Objeto usado para transferir datos entre capas de la aplicación.
- **Paginación:** División de resultados en páginas para mejorar el rendimiento.

### Términos de Negocio

- **Comanda:** Pedido realizado por un usuario para un plato del menú del día.
- **Menú del Día:** Lista de platos disponibles para un día específico, turno y filtros.
- **Turno:** Período del día (Desayuno, Almuerzo, Merienda, Cena).
- **Bonificado:** Comanda que no tiene costo para el usuario.
- **Invitado:** Comanda para un invitado del usuario.
- **Plan Nutricional:** Plan alimentario asignado a un usuario o plato.
- **Jerarquía:** Nivel organizacional del usuario (Gerencia, Supervisión, Operario, etc.).

---

## Anexos

### A. Códigos de Error Comunes

| Código | Mensaje | Solución |
|--------|---------|----------|
| 400 | "Datos inválidos" | Verificar formato y campos requeridos |
| 401 | "Usuario no identificado" | Verificar token JWT válido |
| 401 | "Credenciales inválidas" | Verificar username y password |
| 404 | "Recurso no encontrado" | Verificar ID del recurso |
| 429 | "Demasiados intentos" | Esperar antes de reintentar |
| 500 | "Error interno del servidor" | Contactar al administrador |

### B. Límites y Restricciones

- **Tamaño máximo de página:** 100 registros
- **Tamaño por defecto de página:** 10 registros
- **Rate Limiting:** 5 intentos fallidos por IP cada 15 minutos (solo en login)
- **Tamaño máximo de archivo:** Según configuración del servidor
- **Timeout de solicitudes:** 30 segundos

### C. Mejores Prácticas

1. **Manejo de Tokens:**
   - Almacenar el token de forma segura (no en localStorage si es sensible)
   - Renovar el token antes de que expire
   - Manejar errores 401 y redirigir al login

2. **Paginación:**
   - Usar paginación para listas grandes
   - No solicitar más de 100 registros por página
   - Implementar carga lazy o infinite scroll

3. **Actualización de Datos:**
   - Usar `/api/inicio/web-actualizado` para actualizaciones periódicas
   - Cancelar solicitudes anteriores si cambia el turno
   - Implementar debounce para búsquedas

4. **Manejo de Errores:**
   - Mostrar mensajes de error amigables al usuario
   - Registrar errores para debugging
   - Implementar retry logic para errores transitorios

---

**Fin del Documento**

---

*Este documento fue generado automáticamente. Para actualizaciones, contactar al equipo de desarrollo.*


