-- Tabla para refresh tokens (renovación de JWT sin volver a pedir usuario/contraseña).
-- Ejecutar en la BD existente si no tenés la tabla (ej. después de actualizar el código).

IF OBJECT_ID(N'dbo.sl_refresh_token', N'U') IS NULL
BEGIN
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
    PRINT 'Tabla sl_refresh_token creada.';
END
ELSE
    PRINT 'La tabla sl_refresh_token ya existe.';
