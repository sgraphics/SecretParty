using AzureMapsControl.Components;
using Blazr.RenderState.WASM;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(sp =>
	new HttpClient
	{
	});
builder.AddBlazrRenderStateWASMServices();


builder.Services.AddAzureMapsControl(
	configuration =>
	{
		var key = "9us7lWqyKd-0QjY9WKXcvgx6uxKh63NKq8vCYcOfQOA";
		configuration.SubscriptionKey = key;
	});
await builder.Build().RunAsync();
