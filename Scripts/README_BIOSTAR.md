# 🧪 Scripts de Prueba para Integración con Biostar

Este directorio contiene scripts para probar la integración con Biostar de forma recurrente y crear usuarios de prueba.

## 📋 Archivos

### 1. `ProbarBiostarRecurrente.ps1`
Script PowerShell que llama al endpoint de Biostar cada 2-3 minutos de forma recurrente.

**Uso:**
```powershell
# Ejecutar cada 2 minutos, infinitamente
.\Scripts\ProbarBiostarRecurrente.ps1 -ApiBaseUrl "http://localhost:8000" -IntervaloMinutos 2

# Ejecutar cada 3 minutos, máximo 10 veces
.\Scripts\ProbarBiostarRecurrente.ps1 -ApiBaseUrl "http://localhost:8000" -IntervaloMinutos 3 -MaxEjecuciones 10

# Con log personalizado
.\Scripts\ProbarBiostarRecurrente.ps1 -LogPath ".\Logs\MiTest.log"
```

**Parámetros:**
- `-ApiBaseUrl`: URL base de la API (default: `http://localhost:8000`)
- `-IntervaloMinutos`: Minutos entre cada llamada (default: `2`)
- `-MaxEjecuciones`: Número máximo de ejecuciones, 0 = infinito (default: `0`)
- `-LogPath`: Ruta del archivo de log (default: `.\Logs\BiostarTest.log`)

### 2. `ProbarBiostarUnaVez.ps1`
Script para probar la integración una sola vez (útil para validación rápida).

**Uso:**
```powershell
.\Scripts\ProbarBiostarUnaVez.ps1 -ApiBaseUrl "http://localhost:8000"
```

### 3. `CrearUsuarioPruebaBiostar.sql`
Script SQL para crear un usuario de prueba en la base de datos que coincida con un usuario en Biostar.

**Uso:**
1. Abrir SQL Server Management Studio
2. Conectarse a la base de datos `smartlunch_secure`
3. Abrir el archivo `CrearUsuarioPruebaBiostar.sql`
4. **IMPORTANTE:** Ajustar los valores al inicio del script:
   - `@LegajoPrueba`: Legajo que existe en Biostar
   - `@DniPrueba`: DNI único
   - `@CuilPrueba`: CUIL válido
   - IDs de configuración (Planta, CentroCosto, Proyecto, Jerarquía, PlanNutricional)
5. Ejecutar el script

## 🚀 Guía Rápida

### Paso 1: Crear Usuario de Prueba

1. **Identificar un legajo en Biostar:**
   - Abre Biostar y busca un usuario existente
   - Anota el legajo (ej: `1234`)

2. **Ejecutar el script SQL:**
   ```sql
   -- Editar los valores al inicio del script
   DECLARE @LegajoPrueba INT = 1234;  -- ⚠️ CAMBIAR
   DECLARE @DniPrueba INT = 12345678;
   -- ... etc
   ```

3. **Verificar que el usuario se creó:**
   ```sql
   SELECT * FROM sl_usuario WHERE legajo = 1234;
   ```

### Paso 2: Realizar Fichada en Biostar

1. En Biostar, realiza una fichada con el legajo del usuario de prueba
2. Anota la fecha/hora de la fichada

### Paso 3: Probar la Integración

**Opción A: Prueba rápida (una vez)**
```powershell
.\Scripts\ProbarBiostarUnaVez.ps1
```

**Opción B: Prueba recurrente**
```powershell
# Ejecutar cada 2 minutos
.\Scripts\ProbarBiostarRecurrente.ps1 -IntervaloMinutos 2

# Para detener: Ctrl+C
```

## ⚙️ Configuración Requerida

Antes de ejecutar los scripts, verifica:

### 1. Configuración en `Web.config`:
```xml
<add key="BiostarBaseUrl" value="https://172.16.41.29:4433" />
<add key="BiostarUser" value="smartlunch" />
<add key="BiostarDefaultMinutesBack" value="5" />
<add key="BiostarDefaultDeviceId" value="4865" />
```

### 2. Configuración en `appSettings.secrets.config`:
```xml
<add key="BiostarPassword" value="tu_password" />
```

### 3. IDs de configuración (en `Web.config` o `appSettings.secrets.config`):
```xml
<add key="Planta" value="1" />
<add key="Centro_costo" value="5" />
<add key="Proyecto" value="1" />
<add key="Jerarquia" value="3" />
<add key="Bonificaciones" value="1" />
<add key="Bonificaciones_invitado" value="0" />
```

## 📊 Qué Validar

Los scripts validan:

1. ✅ **Conexión con Biostar**: Que la API pueda conectarse y autenticarse
2. ✅ **Consulta de eventos**: Que se puedan obtener eventos de Biostar
3. ✅ **Registro de fichadas**: Que las fichadas se registren en la BD
4. ✅ **Mapeo de usuarios**: Que los usuarios de Biostar se mapeen correctamente

## 🐛 Troubleshooting

### Error: "La API no está disponible"
- **Solución**: Verifica que la API esté corriendo
- Verifica que la URL sea correcta (`http://localhost:8000` o la que uses)

### Error: "Biostar devolvió un error"
- **Solución**: 
  - Verifica las credenciales en `appSettings.secrets.config`
  - Verifica la URL de Biostar en `Web.config`
  - Verifica que Biostar esté accesible desde el servidor

### "No hay eventos nuevos en Biostar"
- **Causa**: No hubo fichadas en el rango de tiempo configurado
- **Solución**:
  - Realiza una fichada en Biostar
  - Aumenta `BiostarDefaultMinutesBack` en `Web.config`
  - Verifica que el código de evento (`BiostarDefaultDeviceId`) sea correcto

### "No se pudo registrar la fichada"
- **Causa**: El usuario no existe en la BD o hay un error de validación
- **Solución**:
  - Verifica que el legajo del usuario exista en `sl_usuario`
  - Ejecuta el script `CrearUsuarioPruebaBiostar.sql` con el legajo correcto
  - Verifica los logs de la API para más detalles

## 📝 Logs

Los logs se guardan en:
- **Prueba recurrente**: `.\Logs\BiostarTest.log` (o la ruta especificada)
- **Logs de la API**: `App_Data\Logs\smartlunch-api-YYYYMMDD.log`

## 🔄 Automatización

Para ejecutar el script de forma automática (por ejemplo, como tarea programada):

```powershell
# Crear una tarea programada en Windows
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-File `"C:\Ruta\Al\Script\ProbarBiostarRecurrente.ps1`""
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 2) -RepetitionDuration (New-TimeSpan -Hours 24)
Register-ScheduledTask -TaskName "ProbarBiostar" -Action $action -Trigger $trigger
```

## ✅ Checklist de Validación

Antes de considerar la integración como funcional:

- [ ] Usuario de prueba creado en la BD
- [ ] Usuario existe en Biostar con el mismo legajo
- [ ] Fichada realizada en Biostar
- [ ] Script de prueba una vez ejecutado exitosamente
- [ ] Script recurrente ejecutado y funcionando
- [ ] Fichadas se registran correctamente en la BD
- [ ] Logs muestran eventos correctamente

## 📚 Recursos Adicionales

- Documentación de Biostar API
- Logs de la aplicación en `App_Data\Logs\`
- Endpoint de Swagger: `http://localhost:8000/swagger` → `/api/biostar/events`

