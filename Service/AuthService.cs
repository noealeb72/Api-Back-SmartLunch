using System;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using smartlunch_api.App_Start;
using smartlunch_api.Dtos;
using smartlunch_api.Models;
using smartlunch_api.Models.DTOs;
using System.Data.Entity; // importante para Include

namespace smartlunch_api.Services
{
    /// <summary>
    /// Servicio central de autenticación.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly string _jwtSecret;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;

        public AuthService()
        {
            _jwtSecret = ConfigurationManager.AppSettings["JwtSecret"];
            _jwtIssuer = ConfigurationManager.AppSettings["JwtIssuer"];
            _jwtAudience = ConfigurationManager.AppSettings["JwtAudience"];

            if (string.IsNullOrWhiteSpace(_jwtSecret))
                throw new Exception("Falta la clave JwtSecret en web.config.");
        }

        #region IAuthService

        /// <summary>
        /// Login clásico por usuario / contraseña.
        /// Devuelve token + UsuarioDto o null si no valida.
        /// </summary>
        public LoginResponseDto Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                // 1) Traigo SOLO el registro de login
                var login = ctx.sl_login
                    .FirstOrDefault(l =>
                        l.username == username &&
                        !l.deletemark &&
                        l.activo);

                if (login == null)
                    return null;

                // 2) Verifico contraseña
                if (!VerifyPassword(password, login.password_salt, login.password_hash, login.password_iteraciones))
                    return null;

                // 3) Cargo el usuario usando la FK usuario_id (sin Include)
                var usuario = ctx.sl_usuario
                    .FirstOrDefault(u => u.id == login.usuario_id && !u.deletemark);

                if (usuario == null)
                    return null;

                // 4) Mapear a DTO
                var usuarioDto = MapUsuarioToDto(usuario, ctx);
                if (usuarioDto == null)
                    return null;

                // 5) Actualizo last_login
                login.last_login = DateTime.UtcNow;
                ctx.SaveChanges();

                // 6) Genero token
                var token = GenerateJwtToken(login, usuario);

                return new LoginResponseDto
                {
                    Token = token,
                    Usuario = usuarioDto
                };
            }
        }


        /// <summary>
        /// Login para tótem. De momento usa el mismo flujo que Login().
        /// </summary>
        public LoginResponseDto LoginTotem(string username, string password)
        {
            return Login(username, password);
        }

        /// <summary>
        /// Autenticación por legajo (para tótem).
        /// </summary>
        public AuthenticateResponse AuthenticateByLegajo(string legajo)
        {
            if (string.IsNullOrWhiteSpace(legajo))
            {
                return new AuthenticateResponse
                {
                    Success = false,
                    Mensaje = "Legajo requerido."
                };
            }

            if (!int.TryParse(legajo, out var legajoNum))
            {
                return new AuthenticateResponse
                {
                    Success = false,
                    Mensaje = "El legajo debe ser numérico."
                };
            }

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var data =
                    (from u in ctx.sl_usuario
                     join l in ctx.sl_login on u.id equals l.usuario_id
                     where u.legajo == legajoNum
                           && !u.deletemark
                           && !l.deletemark
                           && l.activo
                     select new { Usuario = u, Login = l })
                    .FirstOrDefault();

                if (data == null)
                {
                    return new AuthenticateResponse
                    {
                        Success = false,
                        Mensaje = "No se encontró un usuario activo con ese legajo."
                    };
                }

                var usuarioDto = MapUsuarioToDto(data.Usuario, ctx);
                if (usuarioDto == null)
                {
                    return new AuthenticateResponse
                    {
                        Success = false,
                        Mensaje = "No se pudo armar el usuario."
                    };
                }

                data.Login.last_login = DateTime.UtcNow;
                ctx.SaveChanges();

                var token = GenerateJwtToken(data.Login, data.Usuario);

                return new AuthenticateResponse
                {
                    Success = true,
                    Mensaje = "OK",
                    Token = token,
                    Usuario = usuarioDto
                };
            }
        }

        #endregion

        #region Helpers de mapeo

        /// <summary>
        /// Arma UsuarioDto con IDs y nombres (plan, planta, centro de costo, proyecto, jerarquía).
        /// </summary>
        private UsuarioDto MapUsuarioToDto(sl_usuario u, DataContext ctx)
        {
            if (u == null) return null;

            string nombrePlan = null;
            string nombrePlanta = null;
            string nombreCentro = null;
            string nombreProyecto = null;
            string nombreJerarquia = null;

            if (u.plannutricional_id.HasValue)
            {
                var idPlan = u.plannutricional_id.Value;
                nombrePlan = ctx.sl_plannutricional
                    .Where(p => p.id == idPlan && !p.deletemark)
                    .Select(p => p.nombre)
                    .FirstOrDefault();
            }

            if (u.planta_id.HasValue)
            {
                var idPlanta = u.planta_id.Value;
                nombrePlanta = ctx.sl_planta
                    .Where(p => p.id == idPlanta && !p.deletemark)
                    .Select(p => p.nombre)
                    .FirstOrDefault();
            }

            if (u.centrodecosto_id.HasValue)
            {
                var idCentro = u.centrodecosto_id.Value;
                nombreCentro = ctx.sl_centrodecosto
                    .Where(c => c.id == idCentro && !c.deletemark)
                    .Select(c => c.nombre)
                    .FirstOrDefault();
            }

            if (u.proyecto_id.HasValue)
            {
                var idProyecto = u.proyecto_id.Value;
                nombreProyecto = ctx.sl_proyecto
                    .Where(pj => pj.id == idProyecto && !pj.deletemark)
                    .Select(pj => pj.nombre)
                    .FirstOrDefault();
            }

            if (u.jerarquia_id.HasValue)
            {
                var idJerarquia = u.jerarquia_id.Value;
                nombreJerarquia = ctx.sl_jerarquia
                    .Where(j => j.id == idJerarquia && !j.deletemark)
                    .Select(j => j.nombre)
                    .FirstOrDefault();
            }

            var dto = new UsuarioDto
            {
                id = u.id,
                nombre = u.nombre,
                apellido = u.apellido,
                legajo = u.legajo,
                dni = u.dni,
                cuil = u.cuil,

                plannutricional_id = u.plannutricional_id,
                planta_id = u.planta_id,
                centrodecosto_id = u.centrodecosto_id,
                proyecto_id = u.proyecto_id,
                jerarquia_id = u.jerarquia_id,

                plannutricional_nombre = nombrePlan,
                planta_nombre = nombrePlanta,
                centrodecosto_nombre = nombreCentro,
                proyecto_nombre = nombreProyecto,
                jerarquia_nombre = nombreJerarquia,

                pedido = u.pedidos,
                bonificaciones = u.bonificaciones,
                bonificaciones_invitado = u.bonificaciones_invitado ?? 0,
                llave_acceso = u.llave_acceso
            };

            return dto;
        }

        #endregion

        #region Helpers JWT / Password

        private string GenerateJwtToken(sl_login login, sl_usuario usuario)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_jwtSecret);
            var securityKey = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var role = JerarquiaRolHelper.RolDesdeJerarquia(usuario.jerarquia_id);
            var claims = new[]
            {
                new Claim("login_id",   login.id.ToString()),
                new Claim("usuario_id", usuario.id.ToString()),
                new Claim("legajo",     usuario.legajo.ToString()),
                new Claim(ClaimTypes.Name, $"{usuario.nombre} {usuario.apellido}"),
                new Claim(ClaimTypes.Role, role),
                new Claim("planta_id",        (usuario.planta_id ?? 0).ToString()),
                new Claim("centrodecosto_id", (usuario.centrodecosto_id ?? 0).ToString()),
                new Claim("proyecto_id",      (usuario.proyecto_id ?? 0).ToString()),
                new Claim("jerarquia_id",     (usuario.jerarquia_id ?? 0).ToString())
            };

            var expirationHours = GetJwtExpirationHours();
            var tokenDescriptor = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(expirationHours),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }

        /// <summary>Única fuente de duración del JWT. Lee JwtExpirationHours de appSettings (por defecto 10).</summary>
        private static int GetJwtExpirationHours()
        {
            var value = ConfigurationManager.AppSettings["JwtExpirationHours"];
            return int.TryParse(value, out var h) && h > 0 ? h : 10;
        }

        private bool VerifyPassword(string password, byte[] salt, byte[] hash, int? iteraciones)
        {
            return PasswordUtils.VerificarHash(password, salt, hash, iteraciones);
        }

        #endregion
    }
}
