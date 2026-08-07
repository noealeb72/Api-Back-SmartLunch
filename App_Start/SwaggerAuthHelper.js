(function() {
  var COOKIE_NAME = 'swagger_bearer';
  function normalizarToken(val) {
    var t = (val || '').trim();
    if (t.toLowerCase().indexOf('bearer ') === 0) t = t.slice(7).trim();
    return t;
  }
  function run() {
    if (document.getElementById('swagger-login-helper')) return;
    var div = document.createElement('div');
    div.id = 'swagger-login-helper';
    div.style.cssText = 'padding:12px 20px;margin:0 0 15px;background:#e8f4fd;border:1px solid #b6d9f7;border-radius:4px;font-family:inherit;font-size:14px;';
    div.innerHTML = '<strong>¿Ya te logueaste?</strong> Pega el token (con o sin &quot;Bearer &quot;) y haz clic en el botón.<br>' +
      '<input type="text" id="swagger-token-input" placeholder="Token JWT (con o sin Bearer)" style="width:60%;max-width:400px;margin:8px 8px 8px 0;padding:6px 10px;">' +
      '<button type="button" id="swagger-token-btn" style="padding:6px 14px;cursor:pointer;">Mostrar todos los endpoints</button>';
    var container = document.querySelector('.swagger-ui .information-container') || document.querySelector('.swagger-ui .topbar') || document.querySelector('.swagger-ui');
    if (container) {
      container.insertBefore(div, container.firstChild);
      document.getElementById('swagger-token-btn').onclick = function() {
        var raw = (document.getElementById('swagger-token-input').value || '').trim();
        var token = normalizarToken(raw);
        if (!token) { alert('Pega el token primero.'); return; }
        document.cookie = COOKIE_NAME + '=' + encodeURIComponent(token) + '; path=/; max-age=86400';
        window.location.reload();
      };
    }
  }
  function rellenarAuthorizeConBearer() {
    var match = document.cookie.match(new RegExp('(^| )' + COOKIE_NAME + '=([^;]+)'));
    if (!match) return;
    var token = decodeURIComponent(match[2]).trim();
    if (!token) return;
    if (token.toLowerCase().indexOf('bearer ') === 0) token = token.slice(7).trim();
    var valorHeader = 'Bearer ' + token;
    var btn = document.querySelector('.swagger-ui .auth-wrapper .authorize');
    if (btn) {
      btn.click();
      setTimeout(function() {
        var input = document.querySelector('.swagger-ui .auth-container input');
        if (input) {
          input.value = valorHeader;
          var ev = new Event('input', { bubbles: true });
          input.dispatchEvent(ev);
        }
        var confirmBtn = document.querySelector('.swagger-ui .auth-container .authorize button');
        if (confirmBtn) setTimeout(confirmBtn.click.bind(confirmBtn), 100);
      }, 200);
    }
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run);
  else run();
  setTimeout(rellenarAuthorizeConBearer, 1500);
})();
