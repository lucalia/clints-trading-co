using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[AllowAnonymous]
public class LoginModel(IConfiguration config) : PageModel
{
    public string? Error { get; private set; }
    public string? Username { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string username, string password)
    {
        Username = username;
        var expectedUser = config["Auth:Username"] ?? "";
        var expectedHash = config["Auth:PasswordHash"] ?? "";

        if (!string.Equals(username, expectedUser, StringComparison.OrdinalIgnoreCase) ||
            Sha256Hex(password) != expectedHash)
        {
            Error = "Incorrect email or password.";
            return Page();
        }

        var claims = new[] { new Claim(ClaimTypes.Name, username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return LocalRedirect(Request.Query["ReturnUrl"].FirstOrDefault() ?? "/");
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
