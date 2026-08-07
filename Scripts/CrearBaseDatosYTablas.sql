-- =============================================================================
-- Script: CrearBaseDatosYTablas.sql
-- Crea la base de datos y todas las tablas de SmartLunch API.
--
-- IMPORTANTE:
-- - El nombre de la base debe coincidir con Web.config:
--   - appSettings -> DatabaseName
--   - connectionStrings -> DataContext -> Database=...
-- - Ejecutar una vez contra la instancia SQL Server (ej. .\SQLEXPRESS) antes
--   de levantar la API por primera vez.
-- - Luego, al iniciar la API, DatabaseSeeder insertará catálogos y usuario
--   admin si la BD está vacía (ver App_Start\DatabaseSeeder.cs).
-- =============================================================================

SET NOCOUNT ON;

-- Nombre de la base de datos (cambiar aquí si usas otro en Web.config)
DECLARE @DatabaseName NVARCHAR(128) = N'smartlunch_secure';

-- Crear la base de datos si no existe
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = @DatabaseName)
BEGIN
    DECLARE @Sql NVARCHAR(500) = N'CREATE DATABASE [' + @DatabaseName + N']';
    EXEC sp_executesql @Sql;
    PRINT 'Base de datos creada: ' + @DatabaseName;
END
ELSE
    PRINT 'La base de datos ya existe: ' + @DatabaseName;

-- Usar la base de datos
EXEC('USE [' + @DatabaseName + ']');

-- A partir de aquí las tablas se crean en el contexto de la BD recién usada.
-- Como EXEC cambia de contexto, ejecutar el resto en un bloque dinámico o
-- ejecutar este script ya conectado a la BD, o ejecutar en dos pasos.
-- La forma más simple: que el usuario ejecute primero "USE smartlunch_secure" 
-- o que ejecute el script con la BD ya seleccionada. Para un solo script
-- que cree la BD y luego las tablas, usamos un bloque dinámico para las tablas.

DECLARE @SqlTables NVARCHAR(MAX) = N'
USE [' + @DatabaseName + N'];

-- ========================== TABLAS CATÁLOGO (sin FK o con FK entre sí) ==========================

IF OBJECT_ID(N''dbo.sl_planta'', N''U'') IS NULL
CREATE TABLE dbo.sl_planta (
    id INT IDENTITY(1,1) NOT NULL,
    nombre NVARCHAR(150) NOT NULL,
    descripcion NVARCHAR(300) NULL,
    createdate DATETIME2 NULL,
    createuser NVARCHAR(50) NULL,
    updatedate DATETIME2 NULL,
    updateuser NVARCHAR(50) NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_planta_deletemark DEFAULT 0,
    is_default BIT NOT NULL CONSTRAINT DF_sl_planta_is_default DEFAULT 0,
    CONSTRAINT PK_sl_planta PRIMARY KEY (id),
    CONSTRAINT UQ_sl_planta_nombre UNIQUE (nombre)
);

IF OBJECT_ID(N''dbo.sl_plannutricional'', N''U'') IS NULL
CREATE TABLE dbo.sl_plannutricional (
    id INT IDENTITY(1,1) NOT NULL,
    nombre NVARCHAR(50) NOT NULL,
    descripcion NVARCHAR(150) NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_plannutricional_deletemark DEFAULT 0,
    is_default BIT NOT NULL CONSTRAINT DF_sl_plannutricional_is_default DEFAULT 0,
    createdate DATETIME2 NULL,
    createuser NVARCHAR(50) NULL,
    updatedate DATETIME2 NULL,
    updateuser NVARCHAR(50) NULL,
    CONSTRAINT PK_sl_plannutricional PRIMARY KEY (id)
);

IF OBJECT_ID(N''dbo.sl_centrodecosto'', N''U'') IS NULL
CREATE TABLE dbo.sl_centrodecosto (
    id INT IDENTITY(1,1) NOT NULL,
    planta_id INT NOT NULL,
    nombre NVARCHAR(150) NOT NULL,
    descripcion NVARCHAR(300) NULL,
    createdate DATETIME2 NULL,
    createuser NVARCHAR(50) NULL,
    updatedate DATETIME2 NULL,
    updateuser NVARCHAR(50) NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_centrodecosto_deletemark DEFAULT 0,
    is_default BIT NOT NULL CONSTRAINT DF_sl_centrodecosto_is_default DEFAULT 0,
    CONSTRAINT PK_sl_centrodecosto PRIMARY KEY (id),
    CONSTRAINT FK_sl_centrodecosto_planta FOREIGN KEY (planta_id) REFERENCES dbo.sl_planta(id)
);

IF OBJECT_ID(N''dbo.sl_proyecto'', N''U'') IS NULL
CREATE TABLE dbo.sl_proyecto (
    id INT IDENTITY(1,1) NOT NULL,
    planta_id INT NOT NULL,
    centrodecosto_id INT NOT NULL,
    nombre NVARCHAR(150) NOT NULL,
    descripcion NVARCHAR(300) NULL,
    createdate DATETIME2 NULL,
    createuser NVARCHAR(50) NULL,
    updatedate DATETIME2 NULL,
    updateuser NVARCHAR(50) NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_proyecto_deletemark DEFAULT 0,
    is_default BIT NOT NULL CONSTRAINT DF_sl_proyecto_is_default DEFAULT 0,
    CONSTRAINT PK_sl_proyecto PRIMARY KEY (id),
    CONSTRAINT FK_sl_proyecto_planta FOREIGN KEY (planta_id) REFERENCES dbo.sl_planta(id),
    CONSTRAINT FK_sl_proyecto_centrodecosto FOREIGN KEY (centrodecosto_id) REFERENCES dbo.sl_centrodecosto(id)
);

IF OBJECT_ID(N''dbo.sl_jerarquia'', N''U'') IS NULL
CREATE TABLE dbo.sl_jerarquia (
    id INT IDENTITY(1,1) NOT NULL,
    nombre NVARCHAR(150) NOT NULL,
    descripcion NVARCHAR(150) NULL,
    bonificacion DECIMAL(18,4) NOT NULL,
    createdate DATETIME2 NULL,
    createuser NVARCHAR(50) NULL,
    updatedate DATETIME2 NULL,
    updateuser NVARCHAR(50) NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_jerarquia_deletemark DEFAULT 0,
    is_default BIT NOT NULL CONSTRAINT DF_sl_jerarquia_is_default DEFAULT 0,
    CONSTRAINT PK_sl_jerarquia PRIMARY KEY (id)
);

-- ========================== sl_usuario ==========================

IF OBJECT_ID(N''dbo.sl_usuario'', N''U'') IS NULL
CREATE TABLE dbo.sl_usuario (
    id INT IDENTITY(1,1) NOT NULL,
    nombre NVARCHAR(50) NOT NULL,
    apellido NVARCHAR(50) NOT NULL,
    legajo INT NOT NULL,
    dni INT NOT NULL,
    cuil NVARCHAR(20) NULL,
    domicilio NVARCHAR(100) NULL,
    fechaingreso DATETIME2 NULL,
    contrato NVARCHAR(20) NULL,
    plannutricional_id INT NULL,
    planta_id INT NULL,
    centrodecosto_id INT NULL,
    proyecto_id INT NULL,
    jerarquia_id INT NULL,
    bonificaciones_invitado INT NULL,
    createdate DATETIME2 NULL,
    createuser NVARCHAR(50) NULL,
    updatedate DATETIME2 NULL,
    updateuser NVARCHAR(50) NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_usuario_deletemark DEFAULT 0,
    pedidos INT NULL,
    bonificaciones INT NULL,
    foto NVARCHAR(MAX) NULL,
    email NVARCHAR(100) NULL,
    telefono NVARCHAR(20) NULL,
    llave_acceso NVARCHAR(50) NULL,
    origen_datos NVARCHAR(20) NULL,
    fecha_ultima_sincronizacion DATETIME2 NULL,
    CONSTRAINT PK_sl_usuario PRIMARY KEY (id),
    CONSTRAINT FK_sl_usuario_plannutricional FOREIGN KEY (plannutricional_id) REFERENCES dbo.sl_plannutricional(id),
    CONSTRAINT FK_sl_usuario_planta FOREIGN KEY (planta_id) REFERENCES dbo.sl_planta(id),
    CONSTRAINT FK_sl_usuario_centrodecosto FOREIGN KEY (centrodecosto_id) REFERENCES dbo.sl_centrodecosto(id),
    CONSTRAINT FK_sl_usuario_proyecto FOREIGN KEY (proyecto_id) REFERENCES dbo.sl_proyecto(id),
    CONSTRAINT FK_sl_usuario_jerarquia FOREIGN KEY (jerarquia_id) REFERENCES dbo.sl_jerarquia(id)
);

-- ========================== sl_login ==========================

IF OBJECT_ID(N''dbo.sl_login'', N''U'') IS NULL
CREATE TABLE dbo.sl_login (
    id INT IDENTITY(1,1) NOT NULL,
    usuario_id INT NOT NULL,
    username NVARCHAR(50) NOT NULL,
    password_salt VARBINARY(16) NOT NULL,
    password_hash VARBINARY(32) NOT NULL,
    password_iteraciones INT NULL,
    activo BIT NOT NULL,
    last_login DATETIME2 NULL,
    createdate DATETIME2 NULL,
    createuser NVARCHAR(50) NULL,
    updatedate DATETIME2 NULL,
    updateuser NVARCHAR(50) NULL,
    deletemark BIT NOT NULL,
    must_change_password BIT NOT NULL CONSTRAINT DF_sl_login_must_change_password DEFAULT 0,
    CONSTRAINT PK_sl_login PRIMARY KEY (id),
    CONSTRAINT FK_sl_login_usuario FOREIGN KEY (usuario_id) REFERENCES dbo.sl_usuario(id)
);

-- ========================== sl_refresh_token ==========================

IF OBJECT_ID(N''dbo.sl_refresh_token'', N''U'') IS NULL
CREATE TABLE dbo.sl_refresh_token (
    id INT IDENTITY(1,1) NOT NULL,
    login_id INT NOT NULL,
    token NVARCHAR(128) NOT NULL,
    expires_at DATETIME2 NOT NULL,
    created_at DATETIME2 NOT NULL,
    revoked BIT NOT NULL CONSTRAINT DF_sl_refresh_token_revoked DEFAULT 0,
    CONSTRAINT PK_sl_refresh_token PRIMARY KEY (id),
    CONSTRAINT FK_sl_refresh_token_login FOREIGN KEY (login_id) REFERENCES dbo.sl_login(id),
    CONSTRAINT UQ_sl_refresh_token_token UNIQUE (token)
);

-- ========================== sl_turno ==========================

IF OBJECT_ID(N''dbo.sl_turno'', N''U'') IS NULL
CREATE TABLE dbo.sl_turno (
    id INT IDENTITY(1,1) NOT NULL,
    nombre NVARCHAR(50) NULL,
    horadesde TIME(0) NULL,
    horahasta TIME(0) NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_turno_deletemark DEFAULT 0,
    createdate DATETIME2 NULL,
    createuser NVARCHAR(50) NULL,
    updatedate DATETIME2 NULL,
    updateuser NVARCHAR(50) NULL,
    CONSTRAINT PK_sl_turno PRIMARY KEY (id)
);

-- ========================== sl_plato ==========================

IF OBJECT_ID(N''dbo.sl_plato'', N''U'') IS NULL
CREATE TABLE dbo.sl_plato (
    id INT IDENTITY(1,1) NOT NULL,
    codigo NVARCHAR(150) NOT NULL,
    descripcion NVARCHAR(150) NOT NULL,
    costo DECIMAL(19,4) NOT NULL,
    costo_proveedor DECIMAL(19,4) NOT NULL CONSTRAINT DF_sl_plato_costo_proveedor DEFAULT 0,
    plannutricional_id INT NOT NULL,
    presentacion NVARCHAR(500) NULL,
    ingredientes NVARCHAR(MAX) NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_plato_deletemark DEFAULT 0,
    createdate DATETIME2 NULL,
    createuser NVARCHAR(50) NULL,
    updatedate DATETIME2 NULL,
    updateuser NVARCHAR(50) NULL,
    foto NVARCHAR(500) NULL,
    CONSTRAINT PK_sl_plato PRIMARY KEY (id),
    CONSTRAINT FK_sl_plato_plannutricional FOREIGN KEY (plannutricional_id) REFERENCES dbo.sl_plannutricional(id)
);

-- ========================== sl_menudd ==========================

IF OBJECT_ID(N''dbo.sl_menudd'', N''U'') IS NULL
CREATE TABLE dbo.sl_menudd (
    id INT IDENTITY(1,1) NOT NULL,
    fecha DATETIME2 NOT NULL,
    turno_id INT NOT NULL,
    plato_id INT NOT NULL,
    cantidad INT NOT NULL,
    comandas INT NOT NULL,
    despachado INT NOT NULL,
    planta_id INT NOT NULL,
    centrodecosto_id INT NOT NULL,
    proyecto_id INT NOT NULL,
    jerarquia_id INT NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_menudd_deletemark DEFAULT 0,
    createdate DATETIME2 NULL,
    CONSTRAINT PK_sl_menudd PRIMARY KEY (id),
    CONSTRAINT FK_sl_menudd_turno FOREIGN KEY (turno_id) REFERENCES dbo.sl_turno(id),
    CONSTRAINT FK_sl_menudd_plato FOREIGN KEY (plato_id) REFERENCES dbo.sl_plato(id),
    CONSTRAINT FK_sl_menudd_planta FOREIGN KEY (planta_id) REFERENCES dbo.sl_planta(id),
    CONSTRAINT FK_sl_menudd_centrodecosto FOREIGN KEY (centrodecosto_id) REFERENCES dbo.sl_centrodecosto(id),
    CONSTRAINT FK_sl_menudd_proyecto FOREIGN KEY (proyecto_id) REFERENCES dbo.sl_proyecto(id),
    CONSTRAINT FK_sl_menudd_jerarquia FOREIGN KEY (jerarquia_id) REFERENCES dbo.sl_jerarquia(id)
);

-- ========================== sl_comanda ==========================

IF OBJECT_ID(N''dbo.sl_comanda'', N''U'') IS NULL
CREATE TABLE dbo.sl_comanda (
    id INT IDENTITY(1,1) NOT NULL,
    menudd_id INT NOT NULL,
    npedido INT NOT NULL,
    usuario_id INT NOT NULL,
    plato_id INT NOT NULL,
    turno_id INT NOT NULL,
    fecha DATETIME2 NOT NULL,
    monto DECIMAL(19,4) NOT NULL,
    costo_proveedor DECIMAL(19,4) NOT NULL CONSTRAINT DF_sl_comanda_costo_proveedor DEFAULT 0,
    bonificado BIT NOT NULL,
    invitado BIT NOT NULL,
    calificacion INT NULL,
    estado NVARCHAR(20) NOT NULL,
    comentario NVARCHAR(200) NULL,
    planta_id INT NOT NULL,
    centrodecosto_id INT NOT NULL,
    proyecto_id INT NOT NULL,
    jerarquia_id INT NOT NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_comanda_deletemark DEFAULT 0,
    CONSTRAINT PK_sl_comanda PRIMARY KEY (id),
    CONSTRAINT FK_sl_comanda_menudd FOREIGN KEY (menudd_id) REFERENCES dbo.sl_menudd(id),
    CONSTRAINT FK_sl_comanda_usuario FOREIGN KEY (usuario_id) REFERENCES dbo.sl_usuario(id),
    CONSTRAINT FK_sl_comanda_plato FOREIGN KEY (plato_id) REFERENCES dbo.sl_plato(id),
    CONSTRAINT FK_sl_comanda_turno FOREIGN KEY (turno_id) REFERENCES dbo.sl_turno(id),
    CONSTRAINT FK_sl_comanda_planta FOREIGN KEY (planta_id) REFERENCES dbo.sl_planta(id),
    CONSTRAINT FK_sl_comanda_centrodecosto FOREIGN KEY (centrodecosto_id) REFERENCES dbo.sl_centrodecosto(id),
    CONSTRAINT FK_sl_comanda_proyecto FOREIGN KEY (proyecto_id) REFERENCES dbo.sl_proyecto(id),
    CONSTRAINT FK_sl_comanda_jerarquia FOREIGN KEY (jerarquia_id) REFERENCES dbo.sl_jerarquia(id)
);

-- Genera sl_comanda.npedido sin bloquear (ver Agregar_secuencia_npedido.sql para el detalle
-- de por qué no es una columna IDENTITY: ya hay una en id, y una tabla solo puede tener una).
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N''seq_npedido'' AND schema_id = SCHEMA_ID(N''dbo''))
CREATE SEQUENCE dbo.seq_npedido AS INT START WITH 1 INCREMENT BY 1;

-- ========================== sl_regla_bonificacion ==========================
-- Motor de reglas de bonificación (condiciones opcionales + efecto), administrado
-- por Admin/Gerencia desde /reglas-bonificacion. Ver Agregar_regla_bonificacion.sql
-- para el detalle de por qué existe y la regla de ejemplo que se carga con ella.

IF OBJECT_ID(N''dbo.sl_regla_bonificacion'', N''U'') IS NULL
CREATE TABLE dbo.sl_regla_bonificacion (
    id INT IDENTITY(1,1) NOT NULL,
    nombre NVARCHAR(150) NOT NULL,
    prioridad INT NOT NULL,
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
    CONSTRAINT FK_sl_regla_bonificacion_jerarquia FOREIGN KEY (jerarquia_id) REFERENCES dbo.sl_jerarquia(id),
    CONSTRAINT FK_sl_regla_bonificacion_planta FOREIGN KEY (planta_id) REFERENCES dbo.sl_planta(id),
    CONSTRAINT FK_sl_regla_bonificacion_plannutricional FOREIGN KEY (plannutricional_id) REFERENCES dbo.sl_plannutricional(id)
);

-- Productos a los que aplica una regla (vacío = todos). Relación muchos-a-muchos:
-- una regla puede apuntar a varios productos, un producto puede estar en varias reglas.
IF OBJECT_ID(N''dbo.sl_regla_bonificacion_plato'', N''U'') IS NULL
CREATE TABLE dbo.sl_regla_bonificacion_plato (
    regla_bonificacion_id INT NOT NULL,
    plato_id INT NOT NULL,
    CONSTRAINT PK_sl_regla_bonificacion_plato PRIMARY KEY (regla_bonificacion_id, plato_id),
    CONSTRAINT FK_sl_regla_bonificacion_plato_regla FOREIGN KEY (regla_bonificacion_id) REFERENCES dbo.sl_regla_bonificacion(id) ON DELETE CASCADE,
    CONSTRAINT FK_sl_regla_bonificacion_plato_plato FOREIGN KEY (plato_id) REFERENCES dbo.sl_plato(id)
);

-- Turnos a los que aplica una regla (vacío = todos). Misma lógica que sl_regla_bonificacion_plato.
IF OBJECT_ID(N''dbo.sl_regla_bonificacion_turno'', N''U'') IS NULL
CREATE TABLE dbo.sl_regla_bonificacion_turno (
    regla_bonificacion_id INT NOT NULL,
    turno_id INT NOT NULL,
    CONSTRAINT PK_sl_regla_bonificacion_turno PRIMARY KEY (regla_bonificacion_id, turno_id),
    CONSTRAINT FK_sl_regla_bonificacion_turno_regla FOREIGN KEY (regla_bonificacion_id) REFERENCES dbo.sl_regla_bonificacion(id) ON DELETE CASCADE,
    CONSTRAINT FK_sl_regla_bonificacion_turno_turno FOREIGN KEY (turno_id) REFERENCES dbo.sl_turno(id)
);

IF NOT EXISTS (SELECT 1 FROM dbo.sl_regla_bonificacion WHERE nombre = N''Primer pedido del día (ejemplo)'')
INSERT INTO dbo.sl_regla_bonificacion
    (nombre, prioridad, jerarquia_id, planta_id, plannutricional_id,
     posicion_pedido, es_invitado, fecha_desde, fecha_hasta, tipo_efecto, valor_efecto,
     createdate, createuser, deletemark)
VALUES
    (N''Primer pedido del día (ejemplo)'', 0, NULL, NULL, NULL,
     1, NULL, NULL, NULL, N''Porcentaje'', 66.67,
     GETDATE(), N''Sistema'', 0);

-- ========================== sl_script_ejecutado ==========================
-- Registro de scripts de Scripts\Agregar_*.sql ya ejecutados desde el panel
-- de administración (api/DbScripts), para no volver a ofrecerlos como pendientes.

IF OBJECT_ID(N''dbo.sl_script_ejecutado'', N''U'') IS NULL
CREATE TABLE dbo.sl_script_ejecutado (
    id INT IDENTITY(1,1) NOT NULL,
    nombre_script NVARCHAR(255) NOT NULL,
    fecha_ejecucion DATETIME2 NOT NULL,
    resultado NVARCHAR(20) NOT NULL,
    mensaje NVARCHAR(MAX) NULL,
    CONSTRAINT PK_sl_script_ejecutado PRIMARY KEY (id),
    CONSTRAINT UQ_sl_script_ejecutado_nombre UNIQUE (nombre_script)
);

-- ========================== sl_fichada ==========================

IF OBJECT_ID(N''dbo.sl_fichada'', N''U'') IS NULL
CREATE TABLE dbo.sl_fichada (
    id INT IDENTITY(1,1) NOT NULL,
    identificador_usuario INT NOT NULL,
    turno_id INT NULL,
    fecha_fichada DATETIME2 NOT NULL,
    id_dispositivo INT NOT NULL,
    createdate DATETIME2 NOT NULL,
    event_id BIGINT NOT NULL,
    event_index BIGINT NOT NULL,
    device_ext_id BIGINT NOT NULL,
    device_name NVARCHAR(150) NULL,
    event_code INT NOT NULL,
    CONSTRAINT PK_sl_fichada PRIMARY KEY (id)
);

-- ========================== sl_datosFichada ==========================

IF OBJECT_ID(N''dbo.sl_datosFichada'', N''U'') IS NULL
CREATE TABLE dbo.sl_datosFichada (
    Id INT IDENTITY(1,1) NOT NULL,
    Legajo INT NOT NULL,
    IdLegajoFichada NVARCHAR(50) NULL,
    LlaveAcceso NVARCHAR(100) NULL,
    UltimaFichada DATETIME2 NULL,
    CantidadFichada INT NOT NULL,
    CONSTRAINT PK_sl_datosFichada PRIMARY KEY (Id)
);

-- ========================== sl_configuracion_ ==========================

IF OBJECT_ID(N''dbo.sl_configuracion_'', N''U'') IS NULL
CREATE TABLE dbo.sl_configuracion_ (
    parametro NVARCHAR(50) NOT NULL,
    valor NVARCHAR(10) NOT NULL,
    CONSTRAINT PK_sl_configuracion_ PRIMARY KEY (parametro)
);

-- ========================== sl_configtotem ==========================

IF OBJECT_ID(N''dbo.sl_configtotem'', N''U'') IS NULL
CREATE TABLE dbo.sl_configtotem (
    id INT IDENTITY(1,1) NOT NULL,
    device_id NVARCHAR(100) NOT NULL,
    biostar_modo BIT NOT NULL,
    biostar_interval_segundos INT NOT NULL,
    smarttime_modo BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL,
    deletemark BIT NOT NULL CONSTRAINT DF_sl_configtotem_deletemark DEFAULT 0,
    CONSTRAINT PK_sl_configtotem PRIMARY KEY (id)
);

-- ========================== sl_tokenFichada ==========================

IF OBJECT_ID(N''dbo.sl_tokenFichada'', N''U'') IS NULL
CREATE TABLE dbo.sl_tokenFichada (
    Id INT IDENTITY(1,1) NOT NULL,
    fecha DATETIME2 NULL,
    token NVARCHAR(MAX) NULL,
    CONSTRAINT PK_sl_tokenFichada PRIMARY KEY (Id)
);

-- ========================== sl_fichada_smarttime ==========================

IF OBJECT_ID(N''dbo.sl_fichada_smarttime'', N''U'') IS NULL
CREATE TABLE dbo.sl_fichada_smarttime (
    id INT IDENTITY(1,1) NOT NULL,
    fichada_id_st INT NOT NULL,
    legajo NVARCHAR(50) NULL,
    tipo_operacion NVARCHAR(50) NULL,
    turno_id INT NULL,
    fecha_fichada DATETIME2 NOT NULL,
    id_dispositivo INT NULL,
    id_controladora INT NULL,
    raw_json NVARCHAR(MAX) NULL,
    createdate DATETIME2 NOT NULL,
    CONSTRAINT PK_sl_fichada_smarttime PRIMARY KEY (id)
);

-- ========================== DATOS POR DEFECTO ==========================
-- Planta, Plan nutricional, Centro de costo, Proyecto, Jerarquía, Usuario.
-- El login (admin) se crea al iniciar la API (DatabaseSeeder) para tener
-- la contraseña hasheada correctamente.

IF NOT EXISTS (SELECT 1 FROM dbo.sl_planta WHERE id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.sl_planta ON;
    INSERT INTO dbo.sl_planta (id, nombre, descripcion, createdate, createuser, deletemark, is_default)
    VALUES (1, N''Planta Principal'', N''Planta por defecto'', GETDATE(), N''Seed'', 0, 1);
    SET IDENTITY_INSERT dbo.sl_planta OFF;
END

IF NOT EXISTS (SELECT 1 FROM dbo.sl_plannutricional WHERE id = 2)
BEGIN
    SET IDENTITY_INSERT dbo.sl_plannutricional ON;
    INSERT INTO dbo.sl_plannutricional (id, nombre, descripcion, createdate, createuser, deletemark, is_default)
    VALUES (2, N''Plan estándar'', N''Plan por defecto'', GETDATE(), N''Seed'', 0, 1);
    SET IDENTITY_INSERT dbo.sl_plannutricional OFF;
END

IF NOT EXISTS (SELECT 1 FROM dbo.sl_centrodecosto WHERE id = 5)
BEGIN
    SET IDENTITY_INSERT dbo.sl_centrodecosto ON;
    INSERT INTO dbo.sl_centrodecosto (id, planta_id, nombre, descripcion, createdate, createuser, deletemark, is_default)
    VALUES (5, 1, N''Centro costo principal'', N''Centro por defecto'', GETDATE(), N''Seed'', 0, 1);
    SET IDENTITY_INSERT dbo.sl_centrodecosto OFF;
END

IF NOT EXISTS (SELECT 1 FROM dbo.sl_proyecto WHERE id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.sl_proyecto ON;
    INSERT INTO dbo.sl_proyecto (id, planta_id, centrodecosto_id, nombre, descripcion, createdate, createuser, deletemark, is_default)
    VALUES (1, 1, 5, N''Proyecto principal'', N''Proyecto por defecto'', GETDATE(), N''Seed'', 0, 1);
    SET IDENTITY_INSERT dbo.sl_proyecto OFF;
END

-- Jerarquías: Admin, Cocina, Comensal (default para nuevos usuarios), Gerencia
IF NOT EXISTS (SELECT 1 FROM dbo.sl_jerarquia WHERE id = 1)
BEGIN
    SET IDENTITY_INSERT dbo.sl_jerarquia ON;
    INSERT INTO dbo.sl_jerarquia (id, nombre, descripcion, bonificacion, createdate, createuser, deletemark, is_default)
    VALUES (1, N''Admin'', NULL, 0, GETDATE(), N''Seed'', 0, 0);
    SET IDENTITY_INSERT dbo.sl_jerarquia OFF;
END
IF NOT EXISTS (SELECT 1 FROM dbo.sl_jerarquia WHERE id = 2)
BEGIN
    SET IDENTITY_INSERT dbo.sl_jerarquia ON;
    INSERT INTO dbo.sl_jerarquia (id, nombre, descripcion, bonificacion, createdate, createuser, deletemark, is_default)
    VALUES (2, N''Cocina'', NULL, 0, GETDATE(), N''Seed'', 0, 0);
    SET IDENTITY_INSERT dbo.sl_jerarquia OFF;
END
IF NOT EXISTS (SELECT 1 FROM dbo.sl_jerarquia WHERE id = 3)
BEGIN
    SET IDENTITY_INSERT dbo.sl_jerarquia ON;
    INSERT INTO dbo.sl_jerarquia (id, nombre, descripcion, bonificacion, createdate, createuser, deletemark, is_default)
    VALUES (3, N''Comensal'', NULL, 0, GETDATE(), N''Seed'', 0, 1);
    SET IDENTITY_INSERT dbo.sl_jerarquia OFF;
END
IF NOT EXISTS (SELECT 1 FROM dbo.sl_jerarquia WHERE id = 4)
BEGIN
    SET IDENTITY_INSERT dbo.sl_jerarquia ON;
    INSERT INTO dbo.sl_jerarquia (id, nombre, descripcion, bonificacion, createdate, createuser, deletemark, is_default)
    VALUES (4, N''Gerencia'', NULL, 0, GETDATE(), N''Seed'', 0, 0);
    SET IDENTITY_INSERT dbo.sl_jerarquia OFF;
END

IF NOT EXISTS (SELECT 1 FROM dbo.sl_usuario WHERE legajo = 1 AND deletemark = 0)
BEGIN
    SET IDENTITY_INSERT dbo.sl_usuario ON;
    INSERT INTO dbo.sl_usuario (
        id, nombre, apellido, legajo, dni, cuil, plannutricional_id, planta_id, centrodecosto_id,
        proyecto_id, jerarquia_id, bonificaciones_invitado, bonificaciones, createdate, createuser,
        deletemark, pedidos, origen_datos
    )
    VALUES (
        1, N''Administrador'', N''Sistema'', 1, 11111111, N''20-11111111-9'', 2, 1, 5, 1, 1,
        0, 0, GETDATE(), N''Seed'', 0, 0, N''Seed''
    );
    SET IDENTITY_INSERT dbo.sl_usuario OFF;
END

-- Login admin (usuario_id=1). Contraseña por defecto: smartLunch26 (hash PBKDF2 con salt 16 ceros, 10000 iter, 32 bytes).
IF NOT EXISTS (SELECT 1 FROM dbo.sl_login WHERE username = N''admin'' AND deletemark = 0)
BEGIN
    INSERT INTO dbo.sl_login (usuario_id, username, password_salt, password_hash, activo, createdate, createuser, deletemark, must_change_password)
    VALUES (1, N''admin'', 0x00000000000000000000000000000000, 0x2E78C5076FF2DE1C8EB54ED11D2D7899209A1B74DFCC7F33B13E6EE75AB6E3C5, 1, GETDATE(), N''Seed'', 0, 0);
END

PRINT ''Tablas y datos por defecto listos (planta, plannutricional, centrodecosto, proyecto, jerarquía, usuario, sl_login).'';
PRINT ''Login admin creado con contraseña por defecto: smartLunch26.'';
';

EXEC sp_executesql @SqlTables;

PRINT 'Script finalizado. Base: ' + @DatabaseName;
PRINT 'Asegúrate de que connectionStrings DataContext use Database=' + @DatabaseName;
