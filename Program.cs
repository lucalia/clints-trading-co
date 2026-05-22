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
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open)
        await conn.OpenAsync();

    db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS _MigrationV2Done (Done INTEGER)");

    // Already migrated?
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM _MigrationV2Done";
        var done = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        if (done > 0) return;
    }

    // Check if old schema exists
    if (!await HasTableAsync(conn, "CollectionEntries"))
    {
        db.Database.ExecuteSqlRaw("INSERT INTO _MigrationV2Done VALUES (1)");
        return;
    }

    // ── Migrate Locations ──────────────────────────────────────────────
    var locationMap = new Dictionary<long, string>();
    locationMap[0] = CollectionDbContext.DefaultLocationId;

    if (await HasTableAsync(conn, "Locations"))
    {
        var oldLocs = await ReadRowsAsync(conn, "SELECT Id, Name, Type FROM Locations",
            r => (Id: r.GetInt64(0), Name: r.IsDBNull(1) ? "" : r.GetString(1),
                  Type: r.IsDBNull(2) ? "Box" : r.GetString(2)));
        foreach (var loc in oldLocs) locationMap[loc.Id] = Guid.NewGuid().ToString();

        db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS Locations_v2 (Id TEXT PRIMARY KEY, Name TEXT NOT NULL DEFAULT '', Type TEXT NOT NULL DEFAULT 'Box')");
        foreach (var loc in oldLocs)
            db.Database.ExecuteSqlRaw("INSERT INTO Locations_v2 (Id,Name,Type) VALUES ({0},{1},{2})",
                locationMap[loc.Id], loc.Name, loc.Type);
        db.Database.ExecuteSqlRaw("DROP TABLE Locations");
        db.Database.ExecuteSqlRaw("ALTER TABLE Locations_v2 RENAME TO Locations");
    }

    // ── Migrate Purchases ──────────────────────────────────────────────
    var purchaseMap = new Dictionary<long, string>();
    if (await HasTableAsync(conn, "Purchases"))
    {
        var oldP = await ReadRowsAsync(conn,
            "SELECT Id,PurchasedAt,Source,Description,TotalCost,Quantity,Type,ReceiptUrl,Notes FROM Purchases",
            r => (Id: r.GetInt64(0), PurchasedAt: r.IsDBNull(1)?"":r.GetString(1),
                  Source: r.IsDBNull(2)?"":r.GetString(2), Desc: r.IsDBNull(3)?"":r.GetString(3),
                  Cost: r.IsDBNull(4)?0m:(decimal)r.GetDouble(4), Qty: r.IsDBNull(5)?1:(int)r.GetInt64(5),
                  Type: r.IsDBNull(6)?"Other":r.GetString(6),
                  Receipt: r.IsDBNull(7)?null:r.GetString(7), Notes: r.IsDBNull(8)?null:r.GetString(8)));
        foreach (var p in oldP) purchaseMap[p.Id] = Guid.NewGuid().ToString();

        db.Database.ExecuteSqlRaw(@"CREATE TABLE Purchases_v2 (Id TEXT PRIMARY KEY, PurchasedAt TEXT NOT NULL DEFAULT (date('now')), Source TEXT NOT NULL DEFAULT '', Description TEXT NOT NULL DEFAULT '', TotalCost REAL NOT NULL DEFAULT 0, Quantity INTEGER NOT NULL DEFAULT 1, Type TEXT NOT NULL DEFAULT 'Other', ReceiptUrl TEXT, Notes TEXT)");
        foreach (var p in oldP)
            db.Database.ExecuteSqlRaw(
                "INSERT INTO Purchases_v2 (Id,PurchasedAt,Source,Description,TotalCost,Quantity,Type,ReceiptUrl,Notes) VALUES ({0},{1},{2},{3},{4},{5},{6},{7},{8})",
                purchaseMap[p.Id], p.PurchasedAt, p.Source, p.Desc, p.Cost, p.Qty, p.Type, p.Receipt, p.Notes);
        db.Database.ExecuteSqlRaw("DROP TABLE Purchases");
        db.Database.ExecuteSqlRaw("ALTER TABLE Purchases_v2 RENAME TO Purchases");
    }

    // ── Migrate CardLists ──────────────────────────────────────────────
    var listMap = new Dictionary<long, (string Guid, string Type)>();
    if (await HasTableAsync(conn, "CardLists"))
    {
        var oldL = await ReadRowsAsync(conn, "SELECT Id,Name,ShareToken,Type FROM CardLists",
            r => (Id: r.GetInt64(0), Name: r.IsDBNull(1)?"":r.GetString(1),
                  Token: r.IsDBNull(2)?null:r.GetString(2), Type: r.IsDBNull(3)?"Standard":r.GetString(3)));
        foreach (var l in oldL) listMap[l.Id] = (Guid.NewGuid().ToString(), l.Type);

        db.Database.ExecuteSqlRaw(@"CREATE TABLE CardLists_v2 (Id TEXT PRIMARY KEY, Name TEXT NOT NULL DEFAULT '', ShareToken TEXT UNIQUE, Type TEXT NOT NULL DEFAULT 'Standard')");
        foreach (var l in oldL)
            db.Database.ExecuteSqlRaw("INSERT INTO CardLists_v2 (Id,Name,ShareToken,Type) VALUES ({0},{1},{2},{3})",
                listMap[l.Id].Guid, l.Name, l.Token, l.Type);
        db.Database.ExecuteSqlRaw("DROP TABLE CardLists");
        db.Database.ExecuteSqlRaw("ALTER TABLE CardLists_v2 RENAME TO CardLists");
    }

    // ── Migrate CollectionEntries → CardInstances ──────────────────────
    db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS CardInstances (Id TEXT PRIMARY KEY, CardId TEXT NOT NULL DEFAULT '', Variant TEXT NOT NULL DEFAULT 'Normal', LocationId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000', PurchaseId TEXT, AddedAt TEXT NOT NULL DEFAULT (datetime('now')))");
    db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS WishlistEntries (Id TEXT PRIMARY KEY, ListId TEXT NOT NULL, CardId TEXT NOT NULL, Variant TEXT NOT NULL DEFAULT 'Normal', WantedCount INTEGER NOT NULL DEFAULT 1)");
    db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ListMembers (ListId TEXT NOT NULL, InstanceId TEXT NOT NULL, PRIMARY KEY (ListId, InstanceId))");

    var oldEntries = await ReadRowsAsync(conn,
        "SELECT CardId, LocationId, Variant, Count FROM CollectionEntries",
        r => (CardId: r.GetString(0), LocId: r.IsDBNull(1)?0L:r.GetInt64(1),
              Variant: r.IsDBNull(2)?"Normal":r.GetString(2), Count: r.IsDBNull(3)?1:(int)r.GetInt64(3)));
    foreach (var e in oldEntries)
    {
        var locGuid = locationMap.TryGetValue(e.LocId, out var g) ? g : CollectionDbContext.DefaultLocationId;
        for (var n = 0; n < e.Count; n++)
            db.Database.ExecuteSqlRaw("INSERT INTO CardInstances (Id,CardId,Variant,LocationId) VALUES ({0},{1},{2},{3})",
                Guid.NewGuid().ToString(), e.CardId, e.Variant, locGuid);
    }

    // ── Migrate Wishlist ListEntries ───────────────────────────────────
    if (await HasTableAsync(conn, "ListEntries"))
    {
        var oldLE = await ReadRowsAsync(conn,
            "SELECT ListId,CardId,Variant,Count FROM ListEntries",
            r => (ListId: r.GetInt64(0), CardId: r.GetString(1),
                  Variant: r.IsDBNull(2)?"Normal":r.GetString(2), Count: r.IsDBNull(3)?1:(int)r.GetInt64(3)));
        foreach (var le in oldLE)
        {
            if (!listMap.TryGetValue(le.ListId, out var lm) || lm.Type != "Wishlist") continue;
            db.Database.ExecuteSqlRaw(
                "INSERT OR IGNORE INTO WishlistEntries (Id,ListId,CardId,Variant,WantedCount) VALUES ({0},{1},{2},{3},{4})",
                Guid.NewGuid().ToString(), lm.Guid, le.CardId, le.Variant, le.Count);
        }
    }

    foreach (var t in new[] { "CollectionEntries", "PurchaseCards", "ListEntries" })
        db.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS {t}");

    db.Database.ExecuteSqlRaw("INSERT INTO _MigrationV2Done VALUES (1)");
}

static async Task<bool> HasTableAsync(System.Data.Common.DbConnection conn, string table)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
    return (long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
}

static async Task<List<T>> ReadRowsAsync<T>(
    System.Data.Common.DbConnection conn, string sql,
    Func<System.Data.Common.DbDataReader, T> map)
{
    var results = new List<T>();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        results.Add(map(reader));
    return results;
}
