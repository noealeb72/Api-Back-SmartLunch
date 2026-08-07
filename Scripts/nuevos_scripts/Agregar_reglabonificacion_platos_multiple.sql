-- =============================================================================
-- Reemplaza la condición "Plato" (un solo producto) del motor de reglas de
-- bonificación por una relación muchos-a-muchos: una regla puede apuntar a
-- varios productos a la vez. Reemplaza a Agregar_plato_regla_bonificacion.sql
-- (ninguna regla llegó a usar esa columna todavía, así que no hace falta
-- migrar datos).
-- =============================================================================

SET NOCOUNT ON;

-- Sacar la columna/FK vieja si existían (del script anterior)
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_sl_regla_bonificacion_plato')
BEGIN
    ALTER TABLE dbo.sl_regla_bonificacion DROP CONSTRAINT FK_sl_regla_bonificacion_plato;
    PRINT 'sl_regla_bonificacion: FK vieja a sl_plato eliminada.';
END

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.sl_regla_bonificacion') AND name = N'plato_id'
)
BEGIN
    ALTER TABLE dbo.sl_regla_bonificacion DROP COLUMN plato_id;
    PRINT 'sl_regla_bonificacion: columna plato_id (single) eliminada.';
END

IF OBJECT_ID(N'dbo.sl_regla_bonificacion_plato', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.sl_regla_bonificacion_plato (
        regla_bonificacion_id INT NOT NULL,
        plato_id INT NOT NULL,
        CONSTRAINT PK_sl_regla_bonificacion_plato PRIMARY KEY (regla_bonificacion_id, plato_id),
        CONSTRAINT FK_sl_regla_bonificacion_plato_regla FOREIGN KEY (regla_bonificacion_id) REFERENCES dbo.sl_regla_bonificacion(id) ON DELETE CASCADE,
        CONSTRAINT FK_sl_regla_bonificacion_plato_plato FOREIGN KEY (plato_id) REFERENCES dbo.sl_plato(id)
    );
    PRINT 'sl_regla_bonificacion_plato: tabla creada.';
END

PRINT 'Script finalizado.';
