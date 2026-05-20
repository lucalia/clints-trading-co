# Clint's Card Shop — AI Context

Blazor Server app (.NET 10, SQLite) for tracking a Pokemon TCG collection.
Runs on http://localhost:5128 via `dotnet run`.

## Stack
- **Frontend:** Blazor Server (`Pages/`) + custom CSS (`wwwroot/css/site.css`)
- **API:** TCGDex REST — `https://api.tcgdex.net/v2/en`
- **DB:** SQLite via EF Core, `collection.db` in project root (auto-created on startup)
- **NuGet:** Must use `--source https://api.nuget.org/v3/index.json` (corporate Azure DevOps feed returns 401)

## Key files
| File | Purpose |
|---|---|
| `Pages/Sets.razor` | Home `/` — lists all 353 sets grouped by series, newest first |
| `Pages/Index.razor` | `/set/{SetId}` — card grid for any set, with collection tracking modal |
| `Pages/Locations.razor` | `/locations` — create/edit/delete storage locations |
| `Services/TcgDexService.cs` | TCGDex API wrapper |
| `Services/CollectionService.cs` | Collection CRUD — key: `(CardId, LocationId, Variant)` |
| `Services/LocationService.cs` | Location CRUD; deleting a location moves cards → Unassigned |
| `Data/CollectionDbContext.cs` | EF Core DbContext — two tables: CollectionEntries, Locations |
| `Models/CardDetail.cs` | Card model + `NumberOrStringConverter` (critical — see Gotchas) |
| `Models/SetResponse.cs` | Set/card brief models incl. `SetBrief` for the sets list |
| `wwwroot/img/energy/` | 8 locally-cropped energy icon PNGs (120×120, transparent circles) |

## DB schema
```sql
CollectionEntries (CardId TEXT, LocationId INT, Variant TEXT, Count INT)
  PK: (CardId, LocationId, Variant)
  LocationId=0 means "Unassigned"

Locations (Id INT AUTOINCREMENT, Name TEXT, Type TEXT)
  Types: Box, Tin, ETB, Binder, Display, Other
```
Schema changes require deleting `collection.db` — EnsureCreated() recreates it.

## Critical gotchas
- **Attack.Damage**: TCGDex returns this as a JSON number (`10`) OR string (`"80+"`). `NumberOrStringConverter` in `CardDetail.cs` handles both. Removing it breaks card detail loading for simple-damage attacks.
- **Logo URLs**: Set logos use `{logo}.webp` — NOT `{logo}/high.webp` (404). Card images use `{image}/high.webp` and `{image}/low.webp`.
- **Set ordering**: Sets are grouped by series extracted from the logo URL path, then reversed newest-first. `SerieOf()` in `Sets.razor` does this extraction.
- **Energy icons**: 8 basic types use local PNGs. Dragon, Fairy, Colorless fall back to letter coins with `TypeColor()`. Crop params if re-generating: cx=50%, cy=53%, r=22% of 274×381px MEE source images.
- **Static file cache**: After changing files in `wwwroot/`, tell the user to hard-refresh (`Ctrl+Shift+R`).

## Collection model
Tracks copies per `(CardId, LocationId, Variant)`. Variant values come from the card's `Variants` object: Normal, Reverse, Holo, FirstEdition, WPromo. Total count per card = sum across all location/variant combos.

## What was last worked on
Energy icon PNGs locally cropped and saved. All major features complete (see OVERVIEW.md).
