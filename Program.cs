using ClintCardShop.Data;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.LogoutPath = "/logout";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient<TcgDexService>();

// On Azure App Service, HOME points to D:\home which is always writable.
// ContentRootPath (wwwroot) may be read-only when deployed as a zip package.
var dbRoot = Environment.GetEnvironmentVariable("HOME") ?? builder.Environment.ContentRootPath;
var dbPath = Path.Combine(dbRoot, "data", "collection.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContextFactory<CollectionDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<CollectionService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<CardListService>();
builder.Services.AddScoped<PurchaseService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();

    {
        // SQLite: EnsureCreated + additive raw SQL migrations to preserve existing data
        db.Database.EnsureCreated();
        // Azure App Service uses Azure Files (network share) which doesn't support SQLite's
        // default WAL journal mode. Delete mode works on all file systems.
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=delete;");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS CardLists (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                Name       TEXT    NOT NULL DEFAULT '',
                ShareToken TEXT,
                Type       TEXT    NOT NULL DEFAULT 'Standard'
            )");
        db.Database.ExecuteSqlRaw(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_CardLists_ShareToken ON CardLists(ShareToken)");
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ListEntries (
                ListId     INTEGER NOT NULL,
                CardId     TEXT    NOT NULL,
                LocationId INTEGER NOT NULL DEFAULT 0,
                Variant    TEXT    NOT NULL DEFAULT 'Normal',
                Count      INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (ListId, CardId, LocationId, Variant)
            )");
        try { db.Database.ExecuteSqlRaw("ALTER TABLE CardLists ADD COLUMN Type TEXT NOT NULL DEFAULT 'Standard'"); } catch { }
        try { db.Database.ExecuteSqlRaw("SELECT LocationId FROM PurchaseCards LIMIT 0"); }
        catch
        {
            db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS PurchaseCards");
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE PurchaseCards (
                    PurchaseId INTEGER NOT NULL, CardId TEXT NOT NULL,
                    LocationId INTEGER NOT NULL DEFAULT 0,
                    Variant    TEXT    NOT NULL DEFAULT 'Normal',
                    Quantity   INTEGER NOT NULL DEFAULT 1,
                    PRIMARY KEY (PurchaseId, CardId, LocationId, Variant)
                )");
        }
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS PurchaseCards (
                PurchaseId INTEGER NOT NULL, CardId TEXT NOT NULL,
                LocationId INTEGER NOT NULL DEFAULT 0,
                Variant    TEXT    NOT NULL DEFAULT 'Normal',
                Quantity   INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (PurchaseId, CardId, LocationId, Variant)
            )");
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ApiCache (
                Url      TEXT PRIMARY KEY,
                Body     TEXT NOT NULL DEFAULT '',
                CachedAt TEXT NOT NULL DEFAULT (datetime('now'))
            )");
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS Purchases (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                PurchasedAt TEXT    NOT NULL DEFAULT (date('now')),
                Source      TEXT    NOT NULL DEFAULT '',
                Description TEXT    NOT NULL DEFAULT '',
                TotalCost   REAL    NOT NULL DEFAULT 0,
                Quantity    INTEGER NOT NULL DEFAULT 1,
                Type        TEXT    NOT NULL DEFAULT 'Other',
                ReceiptUrl  TEXT,
                Notes       TEXT
            )");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
