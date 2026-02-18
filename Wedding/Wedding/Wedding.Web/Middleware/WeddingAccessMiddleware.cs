namespace Wedding.Web.Middleware
{
    public class WeddingAccessMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _token;

        public WeddingAccessMiddleware(
            RequestDelegate next,
            IConfiguration configuration)
        {
            _next = next;
            _token = configuration["Wedding:AccessToken"];
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();

            // Rutas públicas
            if (path == "/denied")
            {
                await _next(context);
                return;
            }

            // ¿Tiene cookie?
            if (context.Request.Cookies.ContainsKey("wedding-access"))
            {
                await _next(context);
                return;
            }

            // ¿Viene con token?
            if (context.Request.Query.TryGetValue("token", out var token))
            {
                if (token == _token)
                {
                    context.Response.Cookies.Append(
                        "wedding-access",
                        "ok",
                        new CookieOptions
                        {
                            Expires = DateTimeOffset.UtcNow.AddDays(2),
                            HttpOnly = true,
                            Secure = true
                        });

                    context.Response.Redirect("/");
                    return;
                }
            }

            // Bloqueado
            context.Response.Redirect("/Denied");
        }
    }
}
