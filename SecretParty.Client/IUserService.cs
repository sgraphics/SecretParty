public interface IUserService
{
	Task<string> StartLogin(string email);
	Task<bool> FinishLogin(string token);
	Task SignOut();
}