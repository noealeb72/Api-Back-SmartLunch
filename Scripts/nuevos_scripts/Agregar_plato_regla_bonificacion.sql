-- =============================================================================
-- Agrega la condición "Plato" al motor de reglas de bonificación: permite crear
-- una regla apuntada a un producto puntual (por ejemplo, para excluirlo de
-- cualquier descuento con un efecto Porcentaje 0, con prioridad alta para que
-- gane sobre las reglas más genéricas).
-- =============================================================================

SET NOCOUNT ON;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.sl_regla_bonificacion') AND name = N'plato_id'
)
BEGIN
    ALTER TABLE dbo.sl_regla_bonificacion ADD plato_id INT NULL;
    PRINT 'sl_regla_bonificacion: columna plato_id agregada.';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_sl_regla_bonificacion_plato'
)
BEGIN
    ALTER TABLE dbo.sl_regla_bonificacion
        ADD CONSTRAINT FK_sl_regla_bonificacion_plato FOREIGN KEY (plato_id) REFERENCES dbo.sl_plato(id);
    PRINT 'sl_regla_bonificacion: FK a sl_plato agregada.';
END

PRINT 'Script finalizado.';
