# Uso del Refresh Token en el frontend

## Objetivo

**Que el aplicativo nunca se entere que se deslogueó.** Cuando el JWT expire, el frontend debe renovar la sesión en silencio con el refresh token: el usuario no debe ver pantalla de login ni notar que pasó nada. Solo se debe mostrar login (deslogueo) cuando el refresh token también sea inválido o haya expirado (por ejemplo tras muchos días sin usar la app).

## Flujo recomendado

1. **Al hacer login** (`POST /api/login/Autentificar`): guardar **siempre** tanto `token` (JWT) como `refreshToken` de la respuesta (localStorage/sessionStorage o estado global).

2. **En cada request a la API**: enviar el JWT en el header `Authorization: Bearer <token>`.

3. **Cuando la API responde 401** (JWT expirado o inválido):
   - **No mostrar deslogueo.** No redirigir a login todavía.
   - Llamar en silencio a `POST /api/login/Refresh` con body `{ "refreshToken": "<el refreshToken guardado>" }`.
   - Si Refresh responde **200**: guardar el nuevo `token` y `refreshToken`, **reintentar la petición original** que falló con 401. El usuario no ve nada; la app sigue como si nada hubiera pasado.
   - Solo si Refresh responde **401** (refresh token inválido o expirado): ahí sí limpiar tokens y redirigir a login (recién entonces el usuario “se entera” de que debe volver a iniciar sesión).

4. **Evitar bucles**: marcar la petición de refresh para no volver a intentar refresh si esa misma llamada devuelve 401; en ese caso ir directo a login.

## Endpoints

| Acción | Método | URL | Body | Respuesta 200 |
|--------|--------|-----|------|----------------|
| Login | POST | `/api/login/Autentificar` | `{ "Username", "Password" }` | `{ token, refreshToken, ... }` |
| Refresh | POST | `/api/login/Refresh` | `{ "refreshToken": "..." }` | `{ token, refreshToken }` |

La API devuelve las propiedades en **camelCase** (`token`, `refreshToken`).

## Ejemplo de interceptor (Axios)

```javascript
// Guardar refreshToken al login (además del token)
// Ej: localStorage.setItem('refreshToken', data.refreshToken);

let isRefreshing = false;
let failedQueue = [];

function processQueue(err, token = null) {
  failedQueue.forEach(prom => {
    if (err) prom.reject(err);
    else prom.resolve(token);
  });
  failedQueue = [];
}

axios.interceptors.response.use(
  response => response,
  async error => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then(token => {
          originalRequest.headers['Authorization'] = 'Bearer ' + token;
          return axios(originalRequest);
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const refreshToken = localStorage.getItem('refreshToken');
      if (!refreshToken) {
        isRefreshing = false;
        redirectToLogin();
        return Promise.reject(error);
      }

      try {
        const { data } = await axios.post('/api/login/Refresh', { refreshToken });
        const newToken = data.token;
        const newRefreshToken = data.refreshToken;
        localStorage.setItem('token', newToken);
        localStorage.setItem('refreshToken', newRefreshToken);
        processQueue(null, newToken);
        originalRequest.headers['Authorization'] = 'Bearer ' + newToken;
        return axios(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError, null);
        redirectToLogin();
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);

function redirectToLogin() {
  localStorage.removeItem('token');
  localStorage.removeItem('refreshToken');
  window.location.href = '/login?session=expired';
}
```

## Resumen

- **Objetivo:** que el aplicativo nunca se entere que se deslogueó; la renovación con refresh debe ser transparente.
- **Comportamiento:** JWT expira → 401 → llamar a Refresh con `refreshToken` en silencio → si 200, actualizar tokens y reintentar la petición (el usuario no ve nada). Solo si Refresh devuelve 401, recién ahí redirigir a login y limpiar sesión.
- El backend ya devuelve `refreshToken` en el login y expone `POST /api/login/Refresh`; el cambio es solo en el frontend (interceptor que ante 401 intente refresh y reintente antes de desloguear).
