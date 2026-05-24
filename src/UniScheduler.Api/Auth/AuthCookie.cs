namespace UniScheduler.Api.Auth;

public static class AuthCookie
{
    public const string Name = "uni_session";

    // In dev (localhost SPA + API) the two are same-site, so Lax works and Secure must be off (http).
    // In production the SPA and API are cross-site, which needs SameSite=None + Secure.
    private static (SameSiteMode SameSite, bool Secure) Policy(IHostEnvironment env)
        => env.IsDevelopment()
            ? (SameSiteMode.Lax, false)
            : (SameSiteMode.None, true);

    public static CookieOptions Build(IHostEnvironment env, DateTimeOffset expires)
    {
        var (sameSite, secure) = Policy(env);
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Path = "/",
            Expires = expires
        };
    }

    public static CookieOptions Expired(IHostEnvironment env)
        => Build(env, DateTimeOffset.UtcNow.AddDays(-1));
}
