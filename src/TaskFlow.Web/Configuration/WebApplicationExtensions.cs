using Microsoft.AspNetCore.Diagnostics;
using TaskFlow.Application;

namespace TaskFlow.Web.Configuration;

public static class WebApplicationExtensions
{
    public static WebApplication UseTaskFlowPipeline(this WebApplication app)
    {
        app.UseExceptionHandler(error => error.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            var (status, title) = exception switch
            {
                ValidationException e => (StatusCodes.Status400BadRequest, e.Message),
                ConflictException e => (StatusCodes.Status409Conflict, e.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again.")
            };

            await Results.Problem(statusCode: status, title: title).ExecuteAsync(context);
        }));

        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "same-origin";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Headers.CacheControl = "no-cache, no-store";
                context.Response.Headers.Pragma = "no-cache";
            }

            await next();
        });

        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapTaskFlowEndpoints(this WebApplication app)
    {
        app.MapControllers();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

        return app;
    }
}
