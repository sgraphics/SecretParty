using SecretParty.Model;

public interface IUserService
{
	Task<string> StartLogin(string email, string gender);
	Task<bool> FinishLogin(string token);
	Task SignOut();
	Task<Photo> UploadPhoto(string base64Image);
}