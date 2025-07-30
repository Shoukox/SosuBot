using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = new PhysicalFileProvider(builder.Environment.ContentRootPath + "/danser/videos"),
    RequestPath = "/videos"
});

app.Run();