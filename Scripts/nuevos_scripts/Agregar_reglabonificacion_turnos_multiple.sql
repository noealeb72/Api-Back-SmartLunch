-- =============================================================================
-- Reemplaza la condición "Turno" (uno solo) del motor de reglas de bonificación
-- por una relación muchos-a-muchos: una regla puede apuntar a varios turnos a
-- la vez, igual que ya se hizo con "Producto" en
-- Agregar_reglabonificacion_platos_multiple.sql.
-- =============================================================================

SET NOCOUNT ON;

-- Sacar la FK/columna vieja (turno_id único) si existían
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_sl_regla_bonificacion_turno')
BEGIN
    ALTER TABLE dbo.sl_regla_bonificacion DROP CONSTRAINT FK_sl_regla_bonificacion_turno;
    PRINT 'sl_regla_bonificacion: FK vieja a sl_turno eliminada.';
END

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.sl_regla_bonificacion') AND name = N'turno_id'
)
BEGIN
    ALTER TABLE dbo.sl_regla_bonificacion DROP COLUMN turno_id;
    PRINT 'sl_regla_bonificacion: columna turno_id (single) eliminada.';
END

IF OBJECT_ID(N'dbo.sl_regla_bonificacion_turno', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.sl_regla_bonificacion_turno (
        regla_bonificacion_id INT NOT NULL,
        turno_id INT NOT NULL,
        CONSTRAINT PK_sl_regla_bonificacion_turno PRIMARY KEY (regla_bonificacion_id, turno_id),
        CONSTRAINT FK_sl_regla_bonificacion_turno_regla FOREIGN KEY (regla_bonificacion_id) REFERENCES dbo.sl_regla_bonificacion(id) ON DELETE CASCADE,
        CONSTRAINT FK_sl_regla_bonificacion_turno_turno FOREIGN KEY (turno_id) REFERENCES dbo.sl_turno(id)
    );
    PRINT 'sl_regla_bonificacion_turno: tabla creada.';
END

PRINT 'Script finalizado.';
