using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NUnit.Framework;
using smartlunch_api.Dtos;

namespace smartlunch_api.Tests
{
    /// <summary>
    /// Tests de validación del DTO de login (recomendación M2 / A3 de auditoría).
    /// </summary>
    [TestFixture]
    public class LoginRequestDtoValidationTests
    {
        [Test]
        public void Username_ExcedeMaxLength_DeberiaFallarValidacion()
        {
            var dto = new LoginRequestDto
            {
                Username = new string('a', 101),
                Password = "clave123"
            };
            var errors = Validar(dto);
            Assert.That(errors, Has.Some.Matches<string>(s => s.Contains("Username") || s.Contains("nombre de usuario")));
        }

        [Test]
        public void Password_ExcedeMaxLength_DeberiaFallarValidacion()
        {
            var dto = new LoginRequestDto
            {
                Username = "user",
                Password = new string('x', 257)
            };
            var errors = Validar(dto);
            Assert.That(errors, Has.Some.Matches<string>(s => s.Contains("Password") || s.Contains("contraseña")));
        }

        [Test]
        public void Username_Vacio_DeberiaFallarValidacion()
        {
            var dto = new LoginRequestDto
            {
                Username = null,
                Password = "clave123"
            };
            var errors = Validar(dto);
            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Password_Vacio_DeberiaFallarValidacion()
        {
            var dto = new LoginRequestDto
            {
                Username = "user",
                Password = null
            };
            var errors = Validar(dto);
            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Dto_Valido_NoDeberiaTenerErrores()
        {
            var dto = new LoginRequestDto
            {
                Username = "usuario_valido",
                Password = "claveSegura123"
            };
            var errors = Validar(dto);
            Assert.That(errors, Is.Empty);
        }

        private static IList<string> Validar(LoginRequestDto dto)
        {
            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(dto, context, results, true);
            var errors = new List<string>();
            foreach (var r in results)
                errors.Add(r.ErrorMessage ?? string.Join(", ", r.MemberNames));
            return errors;
        }
    }
}
