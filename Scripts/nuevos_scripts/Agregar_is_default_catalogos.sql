-- =============================================================================
-- Agrega la columna is_default a las tablas de catálogo (para marcar el valor
-- por defecto desde la base y evitar usar un ID del config que esté dado de baja).
-- Ejecutar en la base ya creada si no tiene la columna.
-- =============================================================================

SET NOCOUNT ON;

IF COL_LENGTH('dbo.sl_planta', 'is_default') IS NULL
BEGIN
    ALTER TABLE dbo.sl_planta ADD is_default BIT NOT NULL CONSTRAINT DF_sl_planta_is_default DEFAULT 0;
    PRINT 'sl_planta: columna is_default agregada.';
END

IF COL_LENGTH('dbo.sl_plannutricional', 'is_default') IS NULL
BEGIN
    ALTER TABLE dbo.sl_plannutricional ADD is_default BIT NOT NULL CONSTRAINT DF_sl_plannutricional_is_default DEFAULT 0;
    PRINT 'sl_plannutricional: columna is_default agregada.';
END

IF COL_LENGTH('dbo.sl_centrodecosto', 'is_default') IS NULL
BEGIN
    ALTER TABLE dbo.sl_centrodecosto ADD is_default BIT NOT NULL CONSTRAINT DF_sl_centrodecosto_is_default DEFAULT 0;
    PRINT 'sl_centrodecosto: columna is_default agregada.';
END

IF COL_LENGTH('dbo.sl_proyecto', 'is_default') IS NULL
BEGIN
    ALTER TABLE dbo.sl_proyecto ADD is_default BIT NOT NULL CONSTRAINT DF_sl_proyecto_is_default DEFAULT 0;
    PRINT 'sl_proyecto: columna is_default agregada.';
END

IF COL_LENGTH('dbo.sl_jerarquia', 'is_default') IS NULL
BEGIN
    ALTER TABLE dbo.sl_jerarquia ADD is_default BIT NOT NULL CONSTRAINT DF_sl_jerarquia_is_default DEFAULT 0;
    PRINT 'sl_jerarquia: columna is_default agregada.';
END

PRINT 'Script finalizado.';
