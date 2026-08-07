-- =============================================================================
-- Script: Agregar columna must_change_password a sl_login
-- Uso: Ejecutar una vez en la base de datos antes de desplegar el código que
--      usa el campo (cambio de contraseña obligatorio).
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.sl_login') AND name = N'must_change_password'
)
BEGIN
    ALTER TABLE dbo.sl_login
    ADD must_change_password bit NOT NULL DEFAULT 0;

    -- 0 = no debe cambiar (comportamiento actual para usuarios existentes)
    -- 1 = debe cambiar clave (ej. usuario creado con clave por defecto desde SmartTime)
END
GO
