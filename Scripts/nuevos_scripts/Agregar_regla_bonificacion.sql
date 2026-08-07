-- =============================================================================
-- Crea la tabla sl_regla_bonificacion (motor de reglas de bonificación,
-- administrable por Admin/Gerencia desde /reglas-bonificacion). Cada regla
-- define condiciones opcionales (turno, jerarquía, planta, plan nutricional,
-- posición del pedido en el día, invitado, vigencia por fecha) y un efecto
-- (porcentaje, monto fijo o costo cero). Si ninguna regla activa matchea un
-- pedido, se cobra el 100% (ver ServicioComanda.CrearConDescuento).
--
-- Incluye una regla de ejemplo con la política por defecto: primer pedido
-- del día con 66.67% de subsidio, sin restricción de turno/jerarquía/comedor
-- (el resto de los pedidos del día se cobran al 100% por no matchear esta
-- regla). Sirve como punto de partida para Admin/Gerencia — IMPORTANTE: si
-- se ejecuta este script y no se ajusta esa regla, todos los primeros
-- pedidos del día quedan subsidiados al 66.67% de entrada; si eso no es lo
-- que se quiere, hay que editarla o desactivarla desde la pantalla antes de
-- que los empleados empiecen a pedir.
-- =============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('dbo.sl_regla_bonificacion', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.sl_regla_bonificacion (
        id INT IDENTITY(1,1) NOT NULL,
        nombre NVARCHAR(150) NOT NULL,
        prioridad INT NOT NULL,
        turno_id INT NULL,
        jerarquia_id INT NULL,
        planta_id INT NULL,
        plannutricional_id INT NULL,
        posicion_pedido INT NULL,
        es_invitado BIT NULL,
        fecha_desde DATETIME2 NULL,
        fecha_hasta DATETIME2 NULL,
        tipo_efecto NVARCHAR(20) NOT NULL,
        valor_efecto DECIMAL(19,4) NULL,
        createdate DATETIME2 NULL,
        createuser NVARCHAR(50) NULL,
        updatedate DATETIME2 NULL,
        updateuser NVARCHAR(50) NULL,
        deletemark BIT NOT NULL CONSTRAINT DF_sl_regla_bonificacion_deletemark DEFAULT 0,
        CONSTRAINT PK_sl_regla_bonificacion PRIMARY KEY (id),
        CONSTRAINT FK_sl_regla_bonificacion_turno FOREIGN KEY (turno_id) REFERENCES dbo.sl_turno(id),
        CONSTRAINT FK_sl_regla_bonificacion_jerarquia FOREIGN KEY (jerarquia_id) REFERENCES dbo.sl_jerarquia(id),
        CONSTRAINT FK_sl_regla_bonificacion_planta FOREIGN KEY (planta_id) REFERENCES dbo.sl_planta(id),
        CONSTRAINT FK_sl_regla_bonificacion_plannutricional FOREIGN KEY (plannutricional_id) REFERENCES dbo.sl_plannutricional(id)
    );
    PRINT 'sl_regla_bonificacion: tabla creada.';
END

IF NOT EXISTS (SELECT 1 FROM dbo.sl_regla_bonificacion WHERE nombre = N'Primer pedido del día (ejemplo)')
BEGIN
    INSERT INTO dbo.sl_regla_bonificacion
        (nombre, prioridad, turno_id, jerarquia_id, planta_id, plannutricional_id,
         posicion_pedido, es_invitado, fecha_desde, fecha_hasta, tipo_efecto, valor_efecto,
         createdate, createuser, deletemark)
    VALUES
        (N'Primer pedido del día (ejemplo)', 0, NULL, NULL, NULL, NULL,
         1, NULL, NULL, NULL, N'Porcentaje', 66.67,
         GETDATE(), N'Sistema', 0);
    PRINT 'sl_regla_bonificacion: regla de ejemplo "Primer pedido del día" cargada (66.67%%, revisar/ajustar antes de usar en producción).';
END

PRINT 'Script finalizado.';
