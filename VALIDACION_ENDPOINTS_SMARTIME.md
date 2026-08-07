# Validación endpoints smarTime

## 1. Código revisado y corrección aplicada

### ServicioSmartTimeUsuario – ListarUsuarios
- **Corrección:** Se agregó filtro `!u.deletemark` en la consulta para que el listado **solo muestre usuarios activos** (no dados de baja). Sin esto aparecían también usuarios con baja lógica.

### Resto del código nuevo
- **SmartTimeController:** Rutas y manejo de excepciones revisados; no se detectaron errores.
- **ServicioSmartTimeUsuario (Crear, Actualizar, DarDeBaja):** Validaciones, transacciones y uso de `OrigenSmarTime` coherentes; no se modificó nada más.
- **DTOs (SmartTimeUsuarioDtos.cs):** Validaciones y tipos correctos; no hay impacto en otros DTOs.

---

## 2. Impacto en el resto del proyecto

### No afectado (no se tocó)
- **ServicioUsuario** – Sigue igual. Crear/editar/eliminar usuario por `api/usuario` no usa smarTime.
- **ServicioLogin** – Sin cambios por estos endpoints.
- **LoginController** – Sin cambios (cambiar clave y login siguen igual).
- **UsuarioController** – Prefijo `api/usuario`; no hay conflicto con `api/smartime`.
- **DataContext, sl_usuario, sl_login** – Solo se usan desde `ServicioSmartTimeUsuario`; no se modificaron modelos ni contexto.
- **PagedResultDto** – Se reutiliza tal cual; no se cambió.
- **ServicioDefaultsCatalogo** – Solo se llama desde smarTime; no se modificó.
- **CuilValidator, PasswordUtils** – Solo uso; sin cambios.

### Integración existente
- **SwaggerScopeDocumentFilter** – Ya permite `/api/smartime/` para scope `smarTime`. Los nuevos endpoints (`GET/PUT/DELETE usuarios`, `usuarios/{legajo}`) quedan bajo ese prefijo y se mostrarán bien en Swagger con token smarTime.

### Rutas
- No hay conflicto: `api/smartime/*` (smarTime) vs `api/usuario/*` (UsuarioController).  
- Mismo path `api/smartime/usuarios` con **GET** (listar) y **POST** (crear) es correcto en Web API por método HTTP.

---

## 3. Pruebas recomendadas

### A) Endpoints smarTime (obligatorio probar)

1. **POST api/smartime/usuarios** (crear)  
   - Con token smarTime.  
   - Body válido (nombre, apellido, legajo, dni, cuil; domicilio/fechaIngreso opcionales).  
   - Ver: 201, sl_usuario y sl_login creados con `createuser`/`origen_datos` = "smarTime", username = DNI, contraseña 12345678.  
   - Casos error: DNI/legajo/CUIL duplicado, CUIL inválido, DNI ya como username.

2. **GET api/smartime/usuarios** (listar)  
   - Con token smarTime.  
   - Sin parámetros y con `page`, `pageSize`, `search`.  
   - Ver: solo usuarios con origen smarTime y **solo activos** (no deletemark).  
   - Ver: que no aparezcan usuarios dados de baja con DELETE.

3. **PUT api/smartime/usuarios/{legajo}** (editar)  
   - Con token smarTime.  
   - Usuario existente creado por smarTime: cambiar nombre, apellido, dni, cuil, domicilio, fechaIngreso.  
   - Ver: 200 y datos actualizados en BD (incl. `fecha_ultima_sincronizacion`, `updateuser` = "smarTime").  
   - Casos error: legajo inexistente, usuario no smarTime, DNI/CUIL duplicado.

4. **DELETE api/smartime/usuarios/{legajo}** (dar de baja)  
   - Con token smarTime.  
   - Usuario smarTime existente.  
   - Ver: 200, `deletemark = true` en sl_usuario y en sus sl_login.  
   - Ver: que ese usuario ya no aparezca en GET listar.  
   - Casos error: legajo inexistente, usuario no smarTime.

5. **GET api/smartime/dato-laboral/{legajo}**  
   - Comprobar que sigue funcionando igual (no se modificó).

### B) Regresión (recomendado probar)

- **Login (POST api/login/Autentificar)** con usuario sistema/totem y con usuario smarTime.  
- **Cambiar clave (PUT api/login/cambiar-clave)** con UsuarioId, ClaveActual, NuevaClave y token.  
- **Usuario normal (api/usuario):** listar, crear, editar, dar de baja un usuario (no smarTime) y ver que todo se comporta igual que antes.

### C) Swagger con scope smarTime

- Loguearse con `SwaggerScope: "smarTime"` y recargar (o usar `/api/swagger/enter?token=...`).  
- Ver que solo se muestran endpoints bajo `api/smartime` (incluidos los nuevos GET/PUT/DELETE usuarios).

---

## 4. Resumen

- **Cambio aplicado:** listado smarTime filtra por `!deletemark` (solo activos).  
- **Impacto:** Los nuevos endpoints smarTime no modifican ServicioUsuario, ServicioLogin, UsuarioController ni modelos; solo agregan rutas bajo `api/smartime` y un servicio dedicado.  
- **Pruebas críticas:** Crear, listar, editar y dar de baja usuarios smarTime; luego una pasada rápida de login, cambiar clave y ABM usuario normal para regresión.
