# Configurar la API para crear la base de datos con el usuario sa

Si entrás a SQL Server con **usuario sa** (autenticación SQL) y querés que la **API** use ese mismo usuario para crear la base de datos cuando alguien usa la configuración inicial (Default.html), tenés que configurar la cadena de conexión con **User ID=sa** y la contraseña.

---

## Cómo se conecta la API a la base de datos

La cadena de conexión va en **Web.config**, dentro de la sección `<connectionStrings>`. El código usa `ConfigurationManager.ConnectionStrings["DataContext"]` (DataContext, DatabaseInitializer, SetupController) y obtiene esa cadena.

---

## Cadena en Web.config

En **Web.config** (o en el Web.config ya publicado en el servidor), la sección `<connectionStrings>` debe tener la cadena **DataContext** con autenticación SQL (usuario **sa**):

```xml
<connectionStrings>
  <add name="DataContext"
       connectionString="Server=.\SQLEXPRESS;Database=smartlunch;User Id=sa;Password=TU_CONTRASEÑA_SA;Persist Security Info=True;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

Reemplazá `TU_CONTRASEÑA_SA` por la contraseña real del usuario **sa**. Si tu servidor no usa cifrado, podés usar `Encrypt=False` en lugar de `Encrypt=True;TrustServerCertificate=True`.

---

## Cadena de conexión de referencia (sa)

Cadena de referencia para copiar en **Web.config** (agregar `Database=smartlunch` si no está):

```
Data Source=.\SQLEXPRESS;Persist Security Info=True;User ID=sa;Password=TU_CONTRASEÑA_SA;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name="SQL Server Management Studio";Command Timeout=0
```

Para la API conviene usar además: `Initial Catalog=smartlunch`, `Pooling=True`, `Min Pool Size=5`, `Max Pool Size=100`, `Connect Timeout=30`, y podés quitar `Application Name` y `Command Timeout=0`.

---

## Después de configurar

Al abrir la **configuración inicial** de la API (la pantalla donde se crean las contraseñas de admin y smarTime), la aplicación usará esa cadena de conexión (con **sa**) para conectarse a **master** y ejecutar el script **Scripts\CrearBaseDatosYTablas.sql**, creando la base de datos y las tablas con el usuario con el que vos entrás (sa).
