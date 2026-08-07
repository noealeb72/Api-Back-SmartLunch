using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using smartlunch_api.App_Start;
using smartlunch_api.Services;

namespace smartlunch_api.Controllers
{
    /// <summary>
    /// Panel para revisar y ejecutar los scripts SQL incrementales pendientes
    /// (Scripts\nuevos_scripts\*.sql). Acceso dual: clave simple (ScriptRunnerKey en
    /// appSettings.secrets.config) para usar la página autocontenida sin login (útil
    /// para quien hace el deploy), o un JWT con rol "Admin" para que el front de la
    /// app pueda llamar a estos mismos endpoints con la sesión ya logueada.
    /// Abrir en el navegador: http://localhost:puerto/api/DbScripts
    /// </summary>
    [AllowAnonymous]
    [RoutePrefix("api/DbScripts")]
    public class DbScriptsController : ApiController
    {
        private static string ClaveConfigurada()
        {
            return ConfigurationManager.AppSettings["ScriptRunnerKey"];
        }

        private static bool ClaveValida(string clave)
        {
            var configurada = ClaveConfigurada();
            return !string.IsNullOrEmpty(configurada) && !string.IsNullOrEmpty(clave) && PasswordUtils.FixedTimeEquals(configurada, clave);
        }

        /// <summary>
        /// True si la petición trae un JWT válido (verificado por el middleware OWIN,
        /// que corre igual aunque el controller sea [AllowAnonymous]) con rol "Admin".
        /// </summary>
        private bool TieneSesionAdmin()
        {
            return User?.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin");
        }

        /// <summary>Acceso permitido si la clave es correcta O si hay una sesión de Admin logueada.</summary>
        private bool AccesoValido(string clave)
        {
            return ClaveValida(clave) || TieneSesionAdmin();
        }

        /// <summary>
        /// Devuelve la página HTML del panel (pide la clave y después lista los scripts).
        /// </summary>
        [HttpGet]
        [Route("")]
        public HttpResponseMessage GetPagina()
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GetHtmlPanel(), Encoding.UTF8, "text/html")
            };
            return resp;
        }

        /// <summary>
        /// Lista los scripts de Scripts\nuevos_scripts\ con su descripción y si ya fueron ejecutados.
        /// </summary>
        [HttpGet]
        [Route("Pendientes")]
        public HttpResponseMessage Pendientes(string clave = null)
        {
            if (!AccesoValido(clave))
            {
                return Request.CreateResponse(HttpStatusCode.Unauthorized, new
                {
                    ok = false,
                    mensaje = "No autorizado. Iniciá sesión con un usuario Admin o ingresá la clave del panel."
                });
            }

            try
            {
                var scripts = DatabaseScriptRunner.ListarScripts();
                return Request.CreateResponse(HttpStatusCode.OK, new { ok = true, scripts });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    ok = false,
                    mensaje = "Error al listar los scripts: " + ex.Message
                });
            }
        }

        /// <summary>
        /// Ejecuta los scripts seleccionados. Un script que falla no interrumpe a los siguientes.
        /// </summary>
        [HttpPost]
        [Route("Ejecutar")]
        public HttpResponseMessage Ejecutar([FromBody] DbScriptsEjecutarRequest model)
        {
            if (model == null || !AccesoValido(model.Clave))
            {
                return Request.CreateResponse(HttpStatusCode.Unauthorized, new
                {
                    ok = false,
                    mensaje = "No autorizado. Iniciá sesión con un usuario Admin o ingresá la clave del panel."
                });
            }

            if (model.Scripts == null || model.Scripts.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    mensaje = "No se seleccionó ningún script."
                });
            }

            try
            {
                var resultados = DatabaseScriptRunner.EjecutarScripts(model.Scripts);
                var todosOk = resultados.All(r => r.Exito);
                return Request.CreateResponse(HttpStatusCode.OK, new { ok = todosOk, resultados });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    ok = false,
                    mensaje = "Error al ejecutar los scripts: " + ex.Message
                });
            }
        }

        private static string GetHtmlPanel()
        {
            return @"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>Scripts pendientes - SmartLunch</title>
    <style>
        * { box-sizing: border-box; }
        body { font-family: Arial, Helvetica, 'Segoe UI', sans-serif; margin: 0; padding: 2rem; background: #f0f2f5; min-height: 100vh; }
        .card { background: #fff; border-radius: 8px; box-shadow: 0 2px 12px rgba(0,0,0,.12); max-width: 760px; margin: 0 auto; overflow: hidden; }
        .card-header { background: #ED3F3F; padding: 1rem 1.5rem; display: flex; align-items: center; gap: .75rem; }
        .card-logo-icon { width: 36px; height: 36px; background: rgba(255,255,255,.25); border-radius: 6px; display: flex; align-items: center; justify-content: center; }
        .card-logo-icon svg { width: 22px; height: 22px; fill: #fff; }
        .card-logo-text { color: #fff; font-size: 1.35rem; font-weight: 700; letter-spacing: .02em; }
        .card-body { padding: 1.75rem; }
        h1 { margin: 0 0 .5rem 0; font-size: 1.3rem; color: #1a1a1a; }
        .sub { color: #666; font-size: .9rem; margin-bottom: 1.25rem; }
        label { display: block; margin-bottom: .35rem; font-weight: 600; color: #333; }
        input[type=""password""] { width: 100%; padding: .6rem .75rem; border: 1px solid #ccc; border-radius: 4px; font-size: 1rem; margin-bottom: 1rem; }
        input[type=""password""]:focus { outline: none; border-color: #ED3F3F; }
        button { padding: .65rem 1.25rem; background: #ED3F3F; color: #fff; border: none; border-radius: 4px; font-size: .95rem; font-weight: 600; cursor: pointer; }
        button:hover { background: #d63535; }
        button:disabled { background: #9e9e9e; cursor: not-allowed; }
        .msg { margin-top: 1rem; padding: .75rem; border-radius: 4px; font-size: .9rem; display: none; }
        .msg.error { background: #ffebee; color: #c62828; display: block; }
        .msg.ok { background: #e8f5e9; color: #2e7d32; display: block; }
        table { width: 100%; border-collapse: collapse; margin-top: .5rem; }
        th, td { text-align: left; padding: .6rem .5rem; border-bottom: 1px solid #eee; font-size: .9rem; vertical-align: top; }
        th { color: #666; font-size: .8rem; text-transform: uppercase; letter-spacing: .03em; }
        .nombre { font-weight: 600; color: #333; font-family: Consolas, monospace; font-size: .85rem; }
        .desc { color: #555; }
        .estado-ok { color: #2e7d32; font-weight: 600; white-space: nowrap; }
        .estado-error { color: #c62828; }
        .estado-pendiente { color: #b26a00; font-weight: 600; }
        .resultado-linea { font-size: .85rem; padding: .25rem 0; }
        .footer-acciones { margin-top: 1.25rem; display: flex; align-items: center; gap: 1rem; }
        #screenLista { display: none; }
        #screenLista.visible { display: block; }
    </style>
</head>
<body>
    <div class=""card"">
        <header class=""card-header"">
            <div class=""card-logo-icon"">
                <svg viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg""><path d=""M11 9H9V2H7v7H5V2H3v7c0 2.12 1.66 3.84 3.75 3.97V22h2.5v-9.03C11.34 12.84 13 11.12 13 9V2h-2v7zm5-3v8h2.5v8H21V2c-2.76 0-5 2.24-5 4z""/></svg>
            </div>
            <span class=""card-logo-text"">SmartLunch</span>
        </header>
        <div class=""card-body"">
            <div id=""screenClave"">
                <h1>Scripts pendientes</h1>
                <p class=""sub"">Ingresá la clave del panel para ver y ejecutar los scripts de base de datos pendientes.</p>
                <form id=""fClave"">
                    <label for=""clave"">Clave del panel</label>
                    <input type=""password"" id=""clave"" name=""clave"" required autocomplete=""off"" placeholder=""Ingrese la clave"" />
                    <button type=""submit"" id=""btnClave"">Ingresar</button>
                </form>
                <div id=""msgClave"" class=""msg""></div>
            </div>

            <div id=""screenLista"">
                <h1>Scripts pendientes</h1>
                <p class=""sub"">Tildá los scripts que querés ejecutar y confirmá. Los que ya se ejecutaron correctamente quedan bloqueados.</p>
                <table>
                    <thead>
                        <tr><th style=""width:2rem;""></th><th>Script</th><th>Descripción</th><th>Estado</th></tr>
                    </thead>
                    <tbody id=""tbodyScripts""></tbody>
                </table>
                <div class=""footer-acciones"">
                    <button type=""button"" id=""btnEjecutar"">Ejecutar seleccionados</button>
                    <span id=""contadorSeleccionados"" class=""sub""></span>
                </div>
                <div id=""resultados""></div>
                <div id=""msgLista"" class=""msg""></div>
            </div>
        </div>
    </div>

    <script>
        (function () {
            var screenClave = document.getElementById('screenClave');
            var screenLista = document.getElementById('screenLista');
            var fClave = document.getElementById('fClave');
            var claveInput = document.getElementById('clave');
            var btnClave = document.getElementById('btnClave');
            var msgClave = document.getElementById('msgClave');
            var tbody = document.getElementById('tbodyScripts');
            var btnEjecutar = document.getElementById('btnEjecutar');
            var contador = document.getElementById('contadorSeleccionados');
            var resultadosDiv = document.getElementById('resultados');
            var msgLista = document.getElementById('msgLista');
            var claveActual = '';

            function showMsg(el, text, isError) {
                el.textContent = text;
                el.className = 'msg ' + (isError ? 'error' : 'ok');
                el.style.display = 'block';
            }

            function hideMsg(el) {
                el.style.display = 'none';
            }

            function escapeHtml(s) {
                var d = document.createElement('div');
                d.textContent = s == null ? '' : String(s);
                return d.innerHTML;
            }

            function renderScripts(scripts) {
                tbody.innerHTML = '';
                scripts.forEach(function (s) {
                    var tr = document.createElement('tr');

                    var tdCheck = document.createElement('td');
                    var chk = document.createElement('input');
                    chk.type = 'checkbox';
                    chk.value = s.nombreScript;
                    chk.className = 'chkScript';
                    if (s.ejecutado) chk.disabled = true;
                    chk.addEventListener('change', actualizarContador);
                    tdCheck.appendChild(chk);
                    tr.appendChild(tdCheck);

                    var tdNombre = document.createElement('td');
                    tdNombre.className = 'nombre';
                    tdNombre.textContent = s.nombreScript;
                    tr.appendChild(tdNombre);

                    var tdDesc = document.createElement('td');
                    tdDesc.className = 'desc';
                    tdDesc.textContent = s.descripcion;
                    tr.appendChild(tdDesc);

                    var tdEstado = document.createElement('td');
                    if (s.ejecutado) {
                        var fecha = s.fechaEjecucion ? new Date(s.fechaEjecucion).toLocaleString() : '';
                        tdEstado.innerHTML = '<span class=""estado-ok"">&#10003; Ejecutado ' + escapeHtml(fecha) + '</span>';
                    } else if (s.ultimoResultado === 'ERROR') {
                        tdEstado.innerHTML = '<span class=""estado-error"">&#9888; Error: ' + escapeHtml(s.ultimoMensaje || '') + '</span>';
                    } else {
                        tdEstado.innerHTML = '<span class=""estado-pendiente"">Pendiente</span>';
                    }
                    tr.appendChild(tdEstado);

                    tbody.appendChild(tr);
                });
                actualizarContador();
            }

            function actualizarContador() {
                var chks = document.querySelectorAll('.chkScript:checked');
                contador.textContent = chks.length + ' seleccionado(s)';
            }

            function cargarLista() {
                fetch(window.location.origin + '/api/DbScripts/Pendientes?clave=' + encodeURIComponent(claveActual))
                    .then(function (r) { return r.json().then(function (d) { return { status: r.status, body: d }; }); })
                    .then(function (res) {
                        if (res.status !== 200 || !res.body.ok) {
                            showMsg(msgClave, res.body.mensaje || 'No se pudo validar la clave.', true);
                            btnClave.disabled = false;
                            return;
                        }
                        hideMsg(msgClave);
                        screenClave.style.display = 'none';
                        screenLista.classList.add('visible');
                        renderScripts(res.body.scripts || []);
                    })
                    .catch(function () {
                        showMsg(msgClave, 'Error de conexión con el servidor.', true);
                        btnClave.disabled = false;
                    });
            }

            fClave.addEventListener('submit', function (e) {
                e.preventDefault();
                claveActual = claveInput.value;
                btnClave.disabled = true;
                hideMsg(msgClave);
                cargarLista();
            });

            btnEjecutar.addEventListener('click', function () {
                var seleccionados = Array.prototype.slice.call(document.querySelectorAll('.chkScript:checked'))
                    .map(function (c) { return c.value; });
                if (seleccionados.length === 0) {
                    showMsg(msgLista, 'Seleccioná al menos un script.', true);
                    return;
                }
                btnEjecutar.disabled = true;
                hideMsg(msgLista);
                resultadosDiv.innerHTML = '';

                fetch(window.location.origin + '/api/DbScripts/Ejecutar', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ Clave: claveActual, Scripts: seleccionados })
                })
                    .then(function (r) { return r.json(); })
                    .then(function (d) {
                        btnEjecutar.disabled = false;
                        if (d.resultados) {
                            d.resultados.forEach(function (r) {
                                var linea = document.createElement('div');
                                linea.className = 'resultado-linea ' + (r.exito ? 'estado-ok' : 'estado-error');
                                linea.textContent = (r.exito ? '✓ ' : '✗ ') + r.nombreScript + ': ' + r.mensaje;
                                resultadosDiv.appendChild(linea);
                            });
                        }
                        if (!d.ok) {
                            showMsg(msgLista, d.mensaje || 'Alguno de los scripts falló, revisá el detalle arriba.', true);
                        }
                        cargarLista();
                    })
                    .catch(function () {
                        btnEjecutar.disabled = false;
                        showMsg(msgLista, 'Error de conexión con el servidor.', true);
                    });
            });
        })();
    </script>
</body>
</html>";
        }
    }

    /// <summary>
    /// Request para ejecutar scripts seleccionados desde el panel.
    /// </summary>
    public class DbScriptsEjecutarRequest
    {
        public string Clave { get; set; }
        public List<string> Scripts { get; set; }
    }
}
