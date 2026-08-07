-- =============================================================================
-- Agrega la columna costo_proveedor a sl_plato (costo real que factura el
-- proveedor, distinto del precio de lista que ve el empleado en "costo") y a
-- sl_comanda (snapshot del costo de proveedor al momento del pedido, para que
-- los reportes históricos no se muevan si el precio del plato cambia después).
-- Ejecutar en la base ya creada si no tiene las columnas.
-- =============================================================================

SET NOCOUNT ON;

IF COL_LENGTH('dbo.sl_plato', 'costo_proveedor') IS NULL
BEGIN
    ALTER TABLE dbo.sl_plato ADD costo_proveedor DECIMAL(19,4) NOT NULL CONSTRAINT DF_sl_plato_costo_proveedor DEFAULT 0;
    PRINT 'sl_plato: columna costo_proveedor agregada.';
END

IF COL_LENGTH('dbo.sl_comanda', 'costo_proveedor') IS NULL
BEGIN
    ALTER TABLE dbo.sl_comanda ADD costo_proveedor DECIMAL(19,4) NOT NULL CONSTRAINT DF_sl_comanda_costo_proveedor DEFAULT 0;
    PRINT 'sl_comanda: columna costo_proveedor agregada.';
END

PRINT 'Script finalizado.';
