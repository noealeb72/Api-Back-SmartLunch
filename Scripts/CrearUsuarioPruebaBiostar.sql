-- Script SQL para crear un usuario de prueba para validar la integración con Biostar
-- Este usuario tendrá un legajo específico que debe coincidir con un usuario en Biostar

-- ============================================
-- CONFIGURACIÓN: Ajusta estos valores según tu entorno
-- ============================================

DECLARE @LegajoPrueba INT = 9999;  -- ⚠️ CAMBIAR: Legajo que existe en Biostar
DECLARE @DniPrueba INT = 99999999;  -- ⚠️ CAMBIAR: DNI único
DECLARE @CuilPrueba VARCHAR(20) = '20-99999999-9';  -- ⚠️ CAMBIAR: CUIL válido

-- IDs de configuración (ajustar según tu BD)
DECLARE @PlantaId INT = 1;  -- ⚠️ CAMBIAR según tu configuración
DECLARE @CentroCostoId INT = 5;  -- ⚠️ CAMBIAR según tu configuración
DECLARE @ProyectoId INT = 1;  -- ⚠️ CAMBIAR según tu configuración
DECLARE @JerarquiaId INT = 3;  -- ⚠️ CAMBIAR según tu configuración
DECLARE @PlanNutricionalId INT = 2;  -- ⚠️ CAMBIAR según tu configuración

-- ============================================
-- VERIFICAR SI EL USUARIO YA EXISTE
-- ============================================

IF EXISTS (SELECT 1 FROM sl_usuario WHERE legajo = @LegajoPrueba AND deletemark = 0)
BEGIN
    PRINT '⚠️  El usuario con legajo ' + CAST(@LegajoPrueba AS VARCHAR) + ' ya existe.';
    PRINT '   Si deseas recrearlo, primero elimínalo o marca deletemark = 1';
    RETURN;
END

IF EXISTS (SELECT 1 FROM sl_usuario WHERE dni = @DniPrueba AND deletemark = 0)
BEGIN
    PRINT '⚠️  El usuario con DNI ' + CAST(@DniPrueba AS VARCHAR) + ' ya existe.';
    PRINT '   Cambia el DNI en el script o elimina el usuario existente';
    RETURN;
END

-- ============================================
-- CREAR USUARIO DE PRUEBA
-- ============================================

BEGIN TRANSACTION;

BEGIN TRY
    INSERT INTO sl_usuario (
        nombre,
        apellido,
        legajo,
        dni,
        cuil,
        domicilio,
        fechaingreso,
        contrato,
        plannutricional_id,
        planta_id,
        centrodecosto_id,
        proyecto_id,
        jerarquia_id,
        bonificaciones_invitado,
        createdate,
        createuser,
        updatedate,
        updateuser,
        deletemark,
        pedidos,
        bonificaciones,
        email,
        telefono,
        origen_datos
    )
    VALUES (
        'PRUEBA',                    -- nombre
        'BIOSTAR',                   -- apellido
        @LegajoPrueba,               -- legajo (debe coincidir con Biostar)
        @DniPrueba,                  -- dni
        @CuilPrueba,                 -- cuil
        'Dirección de Prueba',       -- domicilio
        GETDATE(),                   -- fechaingreso
        'PRUEBA',                    -- contrato
        @PlanNutricionalId,          -- plannutricional_id
        @PlantaId,                   -- planta_id
        @CentroCostoId,              -- centrodecosto_id
        @ProyectoId,                 -- proyecto_id
        @JerarquiaId,                -- jerarquia_id
        0,                           -- bonificaciones_invitado
        GETDATE(),                   -- createdate
        'SISTEMA',                   -- createuser
        NULL,                        -- updatedate
        NULL,                        -- updateuser
        0,                           -- deletemark (activo)
        0,                           -- pedidos
        0,                           -- bonificaciones
        'prueba.biostar@test.com',   -- email
        NULL,                        -- telefono
        'PRUEBA_BIOSTAR'             -- origen_datos
    );

    DECLARE @UsuarioId INT = SCOPE_IDENTITY();

    PRINT '✅ Usuario de prueba creado exitosamente:';
    PRINT '   ID: ' + CAST(@UsuarioId AS VARCHAR);
    PRINT '   Legajo: ' + CAST(@LegajoPrueba AS VARCHAR);
    PRINT '   Nombre: PRUEBA BIOSTAR';
    PRINT '   DNI: ' + CAST(@DniPrueba AS VARCHAR);
    PRINT '';
    PRINT '⚠️  IMPORTANTE:';
    PRINT '   1. Verifica que el legajo ' + CAST(@LegajoPrueba AS VARCHAR) + ' exista en Biostar';
    PRINT '   2. Realiza una fichada en Biostar con este legajo';
    PRINT '   3. Ejecuta el script ProbarBiostarRecurrente.ps1 para probar la integración';
    PRINT '';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT '❌ Error al crear usuario de prueba:';
    PRINT ERROR_MESSAGE();
    PRINT '';
    PRINT 'Verifica:';
    PRINT '  - Que los IDs de Planta, CentroCosto, Proyecto, Jerarquía y PlanNutricional existan';
    PRINT '  - Que el DNI y Legajo sean únicos';
    PRINT '  - Que el CUIL tenga formato válido';
END CATCH;

-- ============================================
-- OPCIONAL: Crear también un login para este usuario
-- ============================================

-- Descomentar si también quieres crear un login para este usuario
/*
DECLARE @Username VARCHAR(100) = 'prueba_biostar';
DECLARE @Password VARCHAR(100) = '123456';  -- Se hasheará automáticamente

IF NOT EXISTS (SELECT 1 FROM sl_login WHERE username = @Username AND deletemark = 0)
BEGIN
    INSERT INTO sl_login (
        usuario_id,
        username,
        password,
        estado,
        deletemark,
        createdate,
        createuser
    )
    VALUES (
        @UsuarioId,
        @Username,
        -- ⚠️ IMPORTANTE: El password debe estar hasheado. 
        -- Si tu sistema usa BCrypt, MD5, SHA256, etc., hashea el password aquí
        -- Por ahora lo dejamos en texto plano (el servicio lo hasheará)
        @Password,
        1,  -- activo
        0,  -- no eliminado
        GETDATE(),
        'SISTEMA'
    );
    
    PRINT '✅ Login creado:';
    PRINT '   Username: ' + @Username;
    PRINT '   Password: ' + @Password;
END
*/

