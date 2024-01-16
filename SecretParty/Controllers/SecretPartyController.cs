using Microsoft.AspNetCore.Mvc;

namespace SecretParty.Controllers
{

	[Route("api/[controller]")]
	[ApiController]
	public class SecretPartyController(IUserService authenticationService) : ControllerBase
	{
		[HttpGet]
		[Route("authenticateToken")]
		public async Task<ActionResult> AuthenticateToken(string token)
		{
			await authenticationService.FinishLogin(token);
			return Redirect("/chat");
		}

		[HttpGet]
		[Route("signOut")]
		public async Task<ActionResult> SignOut()
		{
			await authenticationService.SignOut();
			return Redirect("/");
		}
	}
}
