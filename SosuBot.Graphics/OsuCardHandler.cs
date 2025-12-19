using Microsoft.AspNetCore.Mvc;

namespace SosuBot.Graphics
{
    public static class OsuCardHandler
    {
        public static async Task<IResult> HandleHttpRequest(HttpContext context)
        {
            
            MemoryStream imageAsStream = new();
            return Results.File(imageAsStream.ToArray());
        }
    }
}
