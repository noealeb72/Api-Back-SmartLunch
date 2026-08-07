using smartlunch_api.Dtos;
using smartlunch_api.Models.DTOs;

namespace smartlunch_api.Services
{
    public interface IAuthService
    {
        LoginResponseDto Login(string username, string password);
        LoginResponseDto LoginTotem(string username, string password);
        AuthenticateResponse AuthenticateByLegajo(string legajo);


    }
}
