using System.Net.Http.Json;
using AzureMapsControl.Components;
using Blazr.RenderState.WASM;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Stripe;
using System.Security.Claims;
using Flurl.Http;
using Microsoft.AspNetCore.Components;
using SecretParty.Model;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(sp =>
	new HttpClient
	{
	});
builder.AddBlazrRenderStateWASMServices();

builder.Services.AddBootstrapBlazor();
builder.Services.AddAzureMapsControl(
	configuration =>
	{
		var key = "9us7lWqyKd-0QjY9WKXcvgx6uxKh63NKq8vCYcOfQOA";
		configuration.SubscriptionKey = key;
	});

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

await builder.Build().RunAsync();

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
	private readonly NavigationManager _navigationManager;

	public CustomAuthenticationStateProvider(NavigationManager navigationManager)
	{
		_navigationManager = navigationManager;
	}

	public override async Task<AuthenticationState> GetAuthenticationStateAsync()
	{
		// Replace with your API endpoint that returns the user's authentication state

		var userInfo = await $"{_navigationManager.BaseUri}api/SecretParty/userInfo".GetJsonAsync<User>();

		var claimsIdentity = new ClaimsIdentity();
		if (userInfo != null)
		{
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Email, userInfo.RowKey),
				new Claim(ClaimTypes.Name, userInfo.RowKey),
				new Claim(ClaimTypes.Gender, userInfo.Gender),
			};

			claimsIdentity = new ClaimsIdentity(claims, "apiauth_type");
		}

		return new AuthenticationState(new ClaimsPrincipal(claimsIdentity));
	}
}