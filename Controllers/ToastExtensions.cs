using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ClintCardShop.Controllers;

public static class ToastExtensions
{
    private static readonly JsonSerializerOptions _opts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void Toast(this Controller ctrl, string message, string type = "info")
    {
        var payload = JsonSerializer.Serialize(new { showToast = new { message, type } }, _opts);
        ctrl.Response.Headers["HX-Trigger-After-Settle"] = payload;
    }

    public static void ToastSuccess(this Controller ctrl, string message) => ctrl.Toast(message, "success");
    public static void ToastError(this Controller ctrl, string message)   => ctrl.Toast(message, "error");
    public static void ToastWarning(this Controller ctrl, string message) => ctrl.Toast(message, "warning");
}
