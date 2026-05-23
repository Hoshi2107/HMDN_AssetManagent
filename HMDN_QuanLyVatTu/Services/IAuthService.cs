namespace HMS.Services
{
    public interface IAuthService
    {
        LoginResult Login(string username, string password);
    }
}
