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
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<TcgDexService>();

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

    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=delete;");
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ApiCache (
            Url      TEXT PRIMARY KEY,
            Body     TEXT NOT NULL DEFAULT '',
            CachedAt TEXT NOT NULL DEFAULT (datetime('now'))
        )");

    await MigrateToGuidSchemaAsync(db);

    // EnsureCreated creates the new schema tables if they don't exist yet
    db.Database.EnsureCreated();
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
app.MapControllers();

app.Run();

// ── Migration: old integer-ID schema → GUID schema ────────────────────────
static async Task MigrateToGuidSchemaAsync(CollectionDbContext db)
{
    // Already migrated if CardInstances table exists
    var alreadyMigrated = await db.Database.ExecuteSqlRawAsync(
        "CREATE TABLE IF NOT EXISTS _MigrationV2Done (Done INTEGER)") >= 0
        && (await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM _MigrationV2Done").ToListAsync()).FirstOrDefault() > 0;

    if (alreadyMigrated) return;

    // Check if old schema exists
    var hasOldSchema = await HasTableAsync(db, "CollectionEntries");
    if (!hasOldSchema)
    {
        // Fresh install — mark migration as done
        await db.Database.ExecuteSqlRawAsync("INSERT INTO _MigrationV2Done VALUES (1)");
        return;
    }

    // ── Migrate Locations ──────────────────────────────────────────────
    var locationMap = new Dictionary<int, string>(); // old int id → new guid
    locationMap[0] = CollectionDbContext.DefaultLocationId; // "Collection" default

    if (await HasTableAsync(db, "Locations"))
    {
        var oldLocs = await db.Database.SqlQueryRaw<OldLocation>(
            "SELECT Id, Name, Type FROM Locations").ToListAsync();
        foreach (var loc in oldLocs)
        {
            var guid = Guid.NewGuid().ToString();
            locationMap[loc.Id] = guid;
        }
        // Create new table with GUID IDs
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Locations_v2 (
                Id   TEXT PRIMARY KEY,
                Name TEXT NOT NULL DEFAULT '',
                Type TEXT NOT NULL DEFAULT 'Box'
            )");
        foreach (var loc in oldLocs)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Locations_v2 (Id, Name, Type) VALUES ({0}, {1}, {2})",
                locationMap[loc.Id], loc.Name, loc.Type ?? "Box");
        }
        await db.Database.ExecuteSqlRawAsync("DROP TABLE Locations");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Locations_v2 RENAME TO Locations");
    }

    // ── Migrate Purchases ──────────────────────────────────────────────
    var purchaseMap = new Dictionary<int, string>();
    if (await HasTableAsync(db, "Purchases"))
    {
        var oldPurchases = await db.Database.SqlQueryRaw<OldPurchase>(
            "SELECT Id, PurchasedAt, Source, Description, TotalCost, Quantity, Type, ReceiptUrl, Notes FROM Purchases").ToListAsync();
        foreach (var p in oldPurchases)
        {
            var guid = Guid.NewGuid().ToString();
            purchaseMap[p.Id] = guid;
        }
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE Purchases_v2 (
                Id          TEXT PRIMARY KEY,
                PurchasedAt TEXT NOT NULL DEFAULT (date('now')),
                Source      TEXT NOT NULL DEFAULT '',
                Description TEXT NOT NULL DEFAULT '',
                TotalCost   REAL NOT NULL DEFAULT 0,
                Quantity    INTEGER NOT NULL DEFAULT 1,
                Type        TEXT NOT NULL DEFAULT 'Other',
                ReceiptUrl  TEXT,
                Notes       TEXT
            )");
        foreach (var p in oldPurchases)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Purchases_v2 (Id,PurchasedAt,Source,Description,TotalCost,Quantity,Type,ReceiptUrl,Notes) VALUES ({0},{1},{2},{3},{4},{5},{6},{7},{8})",
                purchaseMap[p.Id], p.PurchasedAt, p.Source ?? "", p.Description ?? "", p.TotalCost, p.Quantity, p.Type ?? "Other", p.ReceiptUrl, p.Notes);
        }
        await db.Database.ExecuteSqlRawAsync("DROP TABLE Purchases");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Purchases_v2 RENAME TO Purchases");
    }

    // ── Migrate CardLists ──────────────────────────────────────────────
    var listMap = new Dictionary<int, string>();
    if (await HasTableAsync(db, "CardLists"))
    {
        var oldLists = await db.Database.SqlQueryRaw<OldCardList>(
            "SELECT Id, Name, ShareToken, Type FROM CardLists").ToListAsync();
        foreach (var l in oldLists)
        {
            var guid = Guid.NewGuid().ToString();
            listMap[l.Id] = guid;
        }
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE CardLists_v2 (
                Id         TEXT PRIMARY KEY,
                Name       TEXT NOT NULL DEFAULT '',
                ShareToken TEXT UNIQUE,
                Type       TEXT NOT NULL DEFAULT 'Standard'
            )");
        foreach (var l in oldLists)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO CardLists_v2 (Id,Name,ShareToken,Type) VALUES ({0},{1},{2},{3})",
                listMap[l.Id], l.Name ?? "", l.ShareToken, l.Type ?? "Standard");
        }
        await db.Database.ExecuteSqlRawAsync("DROP TABLE CardLists");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE CardLists_v2 RENAME TO CardLists");
    }

    // ── Migrate CollectionEntries → CardInstances (expand by count) ────
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS CardInstances (
            Id         TEXT PRIMARY KEY,
            CardId     TEXT NOT NULL DEFAULT '',
            Variant    TEXT NOT NULL DEFAULT 'Normal',
            LocationId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            PurchaseId TEXT,
            AddedAt    TEXT NOT NULL DEFAULT (datetime('now'))
        )");

    var oldEntries = await db.Database.SqlQueryRaw<OldCollectionEntry>(
        "SELECT CardId, LocationId, Variant, Count FROM CollectionEntries").ToListAsync();
    foreach (var entry in oldEntries)
    {
        var locGuid = locationMap.TryGetValue(entry.LocationId, out var g) ? g : CollectionDbContext.DefaultLocationId;
        for (var n = 0; n < entry.Count; n++)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO CardInstances (Id, CardId, Variant, LocationId) VALUES ({0},{1},{2},{3})",
                Guid.NewGuid().ToString(), entry.CardId, entry.Variant, locGuid);
        }
    }

    // ── Migrate Wishlist ListEntries → WishlistEntries ─────────────────
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS WishlistEntries (
            Id          TEXT PRIMARY KEY,
            ListId      TEXT NOT NULL,
            CardId      TEXT NOT NULL,
            Variant     TEXT NOT NULL DEFAULT 'Normal',
            WantedCount INTEGER NOT NULL DEFAULT 1
        )");

    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ListMembers (
            ListId     TEXT NOT NULL,
            InstanceId TEXT NOT NULL,
            PRIMARY KEY (ListId, InstanceId)
        )");

    if (await HasTableAsync(db, "ListEntries"))
    {
        // Get wishlist IDs in new GUID form
        var wishlistListIds = listMap
            .Where(kv => true) // we'll check type in query
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var oldListEntries = await db.Database.SqlQueryRaw<OldListEntry>(
            @"SELECT le.ListId, le.CardId, le.Variant, le.Count, cl.Type
              FROM ListEntries le
              LEFT JOIN CardLists cl ON cl.Id = le.ListId").ToListAsync();

        foreach (var entry in oldListEntries.Where(e => e.Type == "Wishlist"))
        {
            if (!listMap.TryGetValue(entry.ListId, out var listGuid)) continue;
            await db.Database.ExecuteSqlRawAsync(
                "INSERT OR IGNORE INTO WishlistEntries (Id,ListId,CardId,Variant,WantedCount) VALUES ({0},{1},{2},{3},{4})",
                Guid.NewGuid().ToString(), listGuid, entry.CardId, entry.Variant, entry.Count);
        }
        // Standard list entries cannot be migrated to instances (no way to match specific copies)
    }

    // ── Clean up old tables ────────────────────────────────────────────
    foreach (var t in new[] { "CollectionEntries", "PurchaseCards", "ListEntries" })
        await db.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {t}");

    await db.Database.ExecuteSqlRawAsync("INSERT INTO _MigrationV2Done VALUES (1)");
}

static async Task<bool> HasTableAsync(CollectionDbContext db, string table)
{
    var result = await db.Database.SqlQueryRaw<int>(
        $"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='{table}'").ToListAsync();
    return result.FirstOrDefault() > 0;
}

// ── Temporary types for migration raw SQL reads ───────────────────────────
file record OldLocation(int Id, string Name, string? Type);
file record OldPurchase(int Id, string PurchasedAt, string? Source, string? Description,
    decimal TotalCost, int Quantity, string? Type, string? ReceiptUrl, string? Notes);
file record OldCardList(int Id, string? Name, string? ShareToken, string? Type);
file record OldCollectionEntry(string CardId, int LocationId, string Variant, int Count);
file record OldListEntry(int ListId, string CardId, string Variant, int Count, string? Type);
