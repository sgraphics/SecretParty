using AzureMapsControl.Components;
using Blazr.RenderState.WASM;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(sp =>
	new HttpClient
	{
	});
builder.AddBlazrRenderStateWASMServices();
builder.Services
	.AddScoped<Darnton.Blazor.DeviceInterop.Geolocation.IGeolocationService,
		Darnton.Blazor.DeviceInterop.Geolocation.GeolocationService>();

//This code uses an anonymous authentication
builder.Services.AddAzureMapsControl(
	configuration => configuration.SubscriptionKey = builder.Configuration.GetValue<string>("AzureMapKey"));
await builder.Build().RunAsync();
