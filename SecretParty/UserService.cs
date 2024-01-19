using System.Security.Claims;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity.UI.Services;
using SecretParty.Client;

public class UserService : IUserService
{
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IConfiguration _configuration;
	private readonly IEmailSender _emailSender;
	private TableServiceClient _serviceClient;

	public UserService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IEmailSender emailSender)
	{
		_httpContextAccessor = httpContextAccessor;
		_configuration = configuration;
		_emailSender = emailSender;
		_serviceClient = new TableServiceClient(_configuration["AzureWebJobsStorage"]);
	}

	private async Task<TableClient> GetUsersTable()
	{
		var usersTable = _serviceClient.GetTableClient("Users");
		await usersTable.CreateIfNotExistsAsync();
		return usersTable;
	}

	public async Task<string> StartLogin(string email)
	{
		email = email.ToLower();
		var serviceClient = new TableServiceClient(_configuration["AzureWebJobsStorage"]);
		var tokensTable = serviceClient.GetTableClient("LoginTokens");
		await tokensTable.CreateIfNotExistsAsync();
		var usersTable = serviceClient.GetTableClient("Users");
		await usersTable.CreateIfNotExistsAsync();
		//create token
		var token = new Random().Next(100000000, 999999999).ToString();
		await usersTable.UpsertEntityAsync(new TableEntity(string.Empty, email)
		{
		});
		await tokensTable.UpsertEntityAsync(new TableEntity(email, token)
		{
			{ "Expiry", DateTimeOffset.UtcNow.AddHours(1) },
		});

		var url = $"{_httpContextAccessor.HttpContext?.Request.Path}?token={token}";
		//send email
		await _emailSender.SendEmailAsync(
			email,
			"Login to SecretParty.Club",
			$@"

<div style=""font-family:'gotham-bold','Roboto Bold',Roboto,Helvetica Neue,Helvetica,sans-serif"">
	<p><img src=""https://SecretParty.Club/img/logo.png"" style=""width:250px""></p>
	<p style=""font-size: 32px;font-weight: 500;"">Hi,<br/>To automatically connect to the SecretParty, copy/paste this temporary login code: </p>
	<p style=""text-align:center;padding:7px 20px;color:#001131;font-weight:500;text-decoration:none;font-size:20px;font-family:'gotham-light','Roboto Light',Roboto,Helvetica Neue,Helvetica,sans-serif;line-height:27px;border:1px solid #d5e1e9;box-sizing:border-box;background:#edf4f9""><strong>{token}</strong></p>
	<hr style=""border:1px solid #d5e1e9"">
	<span style=""padding-top:0px;color:#4d5766;font-weight:300;text-decoration:none;font-size:12px;font-family:'gotham-light','Roboto Light',Roboto,Helvetica Neue,Helvetica,sans-serif;line-height:16px;text-align:left"">If you need any assistance using SecretParty.Club, please contact Support@SecretParty.Club.</span>
</div>"
		);

		//retur token
		return token;
	}

	public async Task<bool> FinishLogin(string token)
	{
		// Replace this with your logic to fetch the email from the table storage
		var userEmail = await FetchEmailFromToken(token);

		if (!string.IsNullOrEmpty(userEmail))
		{
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Email, userEmail),
				new Claim(ClaimTypes.Name, userEmail),
				// Add other claims as needed
			};

			var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

			var authProperties = new AuthenticationProperties
			{
				IsPersistent = true,
				AllowRefresh = true,
			};

			await _httpContextAccessor.HttpContext.SignInAsync(
				CookieAuthenticationDefaults.AuthenticationScheme,
				new ClaimsPrincipal(claimsIdentity),
				authProperties);

			return true;
		}

		return false;
	}

	private async Task<string?> FetchEmailFromToken(string token)
	{
		var serviceClient = new TableServiceClient(_configuration["AzureWebJobsStorage"]);
		var tokenTable = serviceClient.GetTableClient("LoginTokens");

		TokenRecord? tokenRecord = null;
		try
		{
			tokenRecord = TokenRecord.FromEntity(await tokenTable.QueryAsync<TableEntity>(@$"RowKey eq '{token}'").FirstOrDefaultAsync());
		}
		catch { }
		if (tokenRecord.IsUsed || tokenRecord.Expiry < DateTimeOffset.UtcNow)
		{
			return null;
		}
		await tokenTable.UpsertEntityAsync(new TableEntity(tokenRecord.Email, token)
		{
			{ "IsUsed", true },
		});

		return tokenRecord.Email;
	}

	public async Task SignOut()
	{
		await _httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
	}
}

public record TokenRecord(string Email, string Token, DateTimeOffset Timestamp, DateTimeOffset Expiry, bool IsUsed)
{
	public static TokenRecord FromEntity(TableEntity entity)
	{
		return new TokenRecord(entity.PartitionKey, entity.RowKey, entity.GetDateTimeOffset("Timestamp") ?? DateTimeOffset.MinValue, entity.GetDateTimeOffset("Expiry") ?? DateTimeOffset.MinValue, entity.GetBoolean("IsUsed").GetValueOrDefault());
	}
}
