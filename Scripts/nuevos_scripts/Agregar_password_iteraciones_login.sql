-- =============================================================================
-- Agrega sl_login.password_iteraciones para poder subir el costo de hashing
-- (PBKDF2) de las contraseñas nuevas sin invalidar los logins existentes.
--
-- Por qué: el número de iteraciones estaba hardcodeado en 10000 en el código,
-- sin guardarse en ningún lado. Para poder subirlo (a 100000, ver
-- PasswordUtils.IteracionesActuales) sin romper el login de todos los usuarios
-- ya creados, cada fila necesita recordar con cuántas iteraciones se generó SU
-- hash. Las filas existentes quedan en NULL (se interpretan como el valor
-- legado de 10000, ver PasswordUtils.VerificarHash); los logins nuevos o con
-- clave cambiada a partir de ahora quedan en 100000.
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.sl_login') AND name = N'password_iteraciones'
)
BEGIN
    ALTER TABLE dbo.sl_login ADD password_iteraciones INT NULL;
    PRINT 'sl_login.password_iteraciones agregada.';
END
ELSE
BEGIN
    PRINT 'sl_login.password_iteraciones ya existía.';
END

PRINT 'Script finalizado.';
