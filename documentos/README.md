# Documentación interna

Notas técnicas del proyecto que no son parte del README principal. A diferencia
de este, son documentos de trabajo — pueden quedar desactualizados con el
tiempo; ante cualquier duda, el código es la fuente de verdad.

| Documento | Qué es |
|---|---|
| [INFORME_AUDITORIA_SEGURIDAD_API.md](INFORME_AUDITORIA_SEGURIDAD_API.md) | Auditoría de seguridad de febrero 2026, con nota de qué se corrigió después. |
| [MANUAL_API_SMARTTIME.md](MANUAL_API_SMARTTIME.md) | Cómo consumir los endpoints de la integración SmartTime (autenticación, crear/listar/editar/dar de baja usuarios). |
| [FRONTEND_REFRESH_TOKEN.md](FRONTEND_REFRESH_TOKEN.md) | Cómo debe usar el frontend el refresh token para renovar la sesión sin desloguear al usuario. |
| [CONFIGURAR_CONEXION_SA.md](CONFIGURAR_CONEXION_SA.md) | Cómo apuntar la cadena de conexión al usuario `sa` de SQL Server para la configuración inicial. |

Ninguno de estos documentos debe contener contraseñas, claves ni datos reales
— solo placeholders (`TU_CONTRASEÑA_SA`, etc.). Si vas a pegar un ejemplo real
para probar algo, sacalo antes de guardar el archivo.
