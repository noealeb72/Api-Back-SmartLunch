-- =============================================================================
-- Reemplaza el cálculo manual de sl_comanda.npedido (SELECT MAX(npedido)+1 FROM
-- sl_comanda WITH (UPDLOCK, HOLDLOCK)) por una SEQUENCE de SQL Server.
--
-- Por qué: ese SELECT con UPDLOCK, HOLDLOCK escanea toda la tabla y bloquea esa
-- operación para CUALQUIER otro pedido que se esté creando al mismo tiempo, sin
-- importar de qué comedor/turno/plato sea — serializa la creación de pedidos de
-- toda la app en ese paso puntual. sl_comanda.id ya es IDENTITY y no tiene este
-- problema, pero no se puede tener dos columnas IDENTITY en la misma tabla, así
-- que se usa una SEQUENCE aparte: SQL Server la genera sin bloquear a nadie más
-- (mismo mecanismo que usa IDENTITY por dentro, pero como objeto independiente).
-- =============================================================================

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'seq_npedido' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    DECLARE @siguiente INT = (SELECT ISNULL(MAX(npedido), 0) + 1 FROM dbo.sl_comanda);
    DECLARE @sql NVARCHAR(MAX) = N'CREATE SEQUENCE dbo.seq_npedido AS INT START WITH ' + CAST(@siguiente AS NVARCHAR(20)) + N' INCREMENT BY 1;';
    EXEC sp_executesql @sql;
    PRINT 'seq_npedido: secuencia creada, arranca en ' + CAST(@siguiente AS NVARCHAR(20)) + '.';
END

PRINT 'Script finalizado.';
