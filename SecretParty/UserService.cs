using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity.UI.Services;
using SecretParty.Model;

public class UserService : IUserService
{
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IConfiguration _configuration;
	private readonly IEmailSender _emailSender;
	private readonly ILogger<UserService> _logger;
	private TableServiceClient _serviceClient;
	private readonly BlobServiceClient _blobServiceClient;

	public UserService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IEmailSender emailSender, ILogger<UserService> logger)
	{
		_httpContextAccessor = httpContextAccessor;
		_configuration = configuration;
		_emailSender = emailSender;
		_logger = logger;
		_serviceClient = new TableServiceClient(_configuration["AzureWebJobsStorage"]);
		_blobServiceClient = new BlobServiceClient(_configuration["AzureWebJobsStorage"]);
	}

	private async Task<TableClient> GetUsersTable()
	{
		var usersTable = _serviceClient.GetTableClient("Users");
		await usersTable.CreateIfNotExistsAsync();
		return usersTable;
	}

	public async Task<string> StartLogin(string email, string gender)
	{
		email = email.ToLower();
		var serviceClient = new TableServiceClient(_configuration["AzureWebJobsStorage"]);
		var tokensTable = serviceClient.GetTableClient("LoginTokens");
		await tokensTable.CreateIfNotExistsAsync();
		var usersTable = serviceClient.GetTableClient("Users");
		await usersTable.CreateIfNotExistsAsync();
		//create token
		var token = new Random().Next(100000000, 999999999).ToString();
		try
		{
			await usersTable.GetEntityAsync<TableEntity>(string.Empty, email);
		}
		catch
		{
			await usersTable.UpsertEntityAsync(new TableEntity(string.Empty, email)
			{
				{ "Gender", gender == "F" ? "F" : "M" },
			});
		}
		await tokensTable.UpsertEntityAsync(new TableEntity(email, token)
		{
			{ "Expiry", DateTimeOffset.UtcNow.AddHours(1) },
		});

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

		var serviceClient = new TableServiceClient(_configuration["AzureWebJobsStorage"]);
		var usersTable = serviceClient.GetTableClient("Users");
		await usersTable.CreateIfNotExistsAsync();
		var user = await usersTable.GetEntityAsync<TableEntity>(string.Empty, userEmail);

		if (!string.IsNullOrEmpty(userEmail))
		{
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Email, userEmail),
				new Claim(ClaimTypes.Name, userEmail),
				new Claim(ClaimTypes.Gender, user.Value.GetString("Gender")),
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

	public async Task<Photo> UploadPhoto(string base64Image)
	{
		try
		{
			var blobContainerName = "partyparticipants";
			var client = _blobServiceClient.GetBlobContainerClient(blobContainerName);
			await client.CreateIfNotExistsAsync(PublicAccessType.Blob);
			var blobName = Guid.NewGuid().ToString().Replace("-", string.Empty);

			var data = Convert.FromBase64String(base64Image);

			using var imageStream = new MemoryStream(data, 0, data.Length);

			var image = Image.FromStream(imageStream);

			var height = (200 * image.Height) / image.Width;

			var thumbnail = image.GetThumbnailImage(200, height, null, IntPtr.Zero);

			var thumbnailStream = new MemoryStream();
			thumbnail.Save(thumbnailStream, ImageFormat.Png);

			thumbnailStream.Position = 0;
			imageStream.Position = 0;

			var photoBlobClient = client.GetBlobClient(blobName + ".png");
			await photoBlobClient.UploadAsync(imageStream);
			await photoBlobClient.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = "image/png" });

			var thumbnailBlobClient = client.GetBlobClient(blobName + "t.png");
			await thumbnailBlobClient.UploadAsync(thumbnailStream);
			await thumbnailBlobClient.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = "image/png" });

			return new Photo
			{
				Url = $"https://{client.AccountName}.blob.core.windows.net/{blobContainerName}/{blobName}.png",
				ThumbUrl = $"https://{client.AccountName}.blob.core.windows.net/{blobContainerName}/{blobName}t.png",
			};
		}
		catch (Exception e)
		{
			_logger?.LogError(e, "Image thumbnail failed: " + e.Message);
			throw;
		}
	}

	private async Task<Stream> GetImageAsByteArray(string urlImage)
	{

		var client = new HttpClient();
		var response = await client.GetAsync(urlImage);

		return await response.Content.ReadAsStreamAsync();
	}

	public static string CreateMD5(string input)
	{
		// Use input string to calculate MD5 hash
		using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
		{
			byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
			byte[] hashBytes = md5.ComputeHash(inputBytes);

			return Convert.ToHexString(hashBytes).Replace("-", string.Empty).ToLower(); // .NET 5 +

			// Convert the byte array to hexadecimal string prior to .NET 5
			// StringBuilder sb = new System.Text.StringBuilder();
			// for (int i = 0; i < hashBytes.Length; i++)
			// {
			//     sb.Append(hashBytes[i].ToString("X2"));
			// }
			// return sb.ToString();
		}
	}
}

public record TokenRecord(string Email, string Token, DateTimeOffset Timestamp, DateTimeOffset Expiry, bool IsUsed)
{
	public static TokenRecord FromEntity(TableEntity entity)
	{
		return new TokenRecord(entity.PartitionKey, entity.RowKey, entity.GetDateTimeOffset("Timestamp") ?? DateTimeOffset.MinValue, entity.GetDateTimeOffset("Expiry") ?? DateTimeOffset.MinValue, entity.GetBoolean("IsUsed").GetValueOrDefault());
	}
}
