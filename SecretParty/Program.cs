using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity.UI.Services;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using SecretParty.Client.Pages;
using SecretParty.Components;
using Stripe;
using SecretParty.Web.Data;
using Blazr.RenderState.Server;
using SecretParty;
using SecretParty.Client;
using AzureMapsControl.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents()
	.AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddBootstrapBlazor();

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddTransient<ChatService>();
builder.Services.AddSingleton<AiProxy>();
builder.Services.AddScoped(sp =>
	new HttpClient
	{
	});
builder.Services.AddAzureMapsControl(
	configuration =>
	{
		var key = "9us7lWqyKd-0QjY9WKXcvgx6uxKh63NKq8vCYcOfQOA";
		configuration.SubscriptionKey = key;
	});

builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.PropertyNamingPolicy = null;
		options.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
		options.JsonSerializerOptions.Converters.Add(NodaConverters.IntervalConverter);
		options.JsonSerializerOptions.Converters.Add(NodaConverters.InstantConverter);
	});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie();
builder.AddBlazrRenderStateServerServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseWebAssemblyDebugging();
}
else
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode()
	.AddInteractiveWebAssemblyRenderMode()
	.AddAdditionalAssemblies(typeof(Counter).Assembly);



StripeConfiguration.ApiKey = builder.Configuration["StripeApi"];

app.Run();
