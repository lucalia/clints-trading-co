# Clint's Card Shop — Project Overview

**Owner:** Clint Warner (clint.warner@nucor.com)  
**Purpose:** Personal Pokemon TCG collection tracker — browse sets, track card copies by location and variant, view card details and pricing.  
**Stack:** C# / Blazor Server / .NET 10 / SQLite / EF Core  
**Data source:** [TCGDex API](https://tcgdex.dev) (public, no auth required)

---

## Features Implemented

### Sets Browser (`/`)
- Loads all 353 sets from the TCGDex API
- Sets are grouped by series (e.g., all Scarlet & Violet sets together, all Mega Evolution sets together) with series ordered newest-first
- Within each series, sets are ordered newest-first
- Searchable by name or set ID
- Each set card shows the official logo image, set name, total card count, and set ID
- Clicking a set navigates to its card grid

### Card Grid (`/set/{setId}`)
- Dynamically loads any set by ID — not hardcoded to a single set
- Displays all cards as a responsive grid of thumbnails (low-quality images for performance)
- Search by card name or number
- Filter chips: **All** / **Owned** / **Missing** (Owned/Missing filter by whether the user has any copies in their collection)
- Collection count badge (×N) on each tile when copies are tracked
- Clicking a card opens a detail modal
- Back link to Sets browser

### Card Detail Modal
- Full high-quality card image
- Card name, HP, type badges, stage, rarity
- **Trainer cards:** displays card text (effect) and Trainer subtype badge (Supporter, Item, Stadium, Tool)
- **Abilities** section with ability name, type, and effect text
- **Attacks** section with energy cost coins, attack name, damage, and effect text
- **Energy cost icons:** 8 basic types use real cropped energy card images (from MEE set, saved locally). Dragon, Fairy, and Colorless use letter-based fallback coins.
- Pricing (USD estimate, converted from Cardmarket EUR at ~1.09 rate, or "No pricing data" message)
- Illustrator, retreat cost, regulation mark
- **My Collection** tracking section (see below)

### Collection Tracking
- Tracks copies per card × location × variant
- **Variant selector:** shows only variants the card actually has (Normal, Reverse, Holo, FirstEdition, WPromo) — sourced from the API's `variants` object
- **Location dropdown:** choose which location to adjust (Unassigned by default)
- **Count controls:** − / editable number input (type any count directly) / + 
- **Breakdown summary:** scrollable list of all non-zero (variant • location) assignments, capped at a fixed height so controls never jump when rows are added
- Total copy count displayed
- Collection badge on grid tiles updates in real time

### Locations Management (`/locations`)
- Create named storage locations with a type: Box, Tin, ETB, Binder, Display, Other
- Edit location name and type inline
- Delete a location — all cards assigned to it automatically move to Unassigned
- Shows how many card copies are stored at each location
- Duplicate name validation

---

## Architecture Decisions

### Why Blazor Server (not WASM)?
Blazor Server runs all logic on the server, so API calls to TCGDex happen server-side with no CORS issues. WebAssembly would require a proxy or CORS headers from TCGDex.

### Why SQLite?
Local, file-based, zero-configuration. Perfect for a single-user desktop app. The database file (`collection.db`) lives in the project root and is auto-created by `EnsureCreated()` on startup.

### Collection key design
The composite primary key `(CardId, LocationId, Variant)` allows tracking independent copy counts for each combination — e.g., 2 Normal copies in Box A, 1 Reverse copy in a Binder, 1 Holo copy Unassigned — all for the same card.

### Energy icons
The 8 basic energy types use real Pokemon energy card artwork (from the MEE — Mega Evolution Energy — set, 2025). Images are downloaded and cropped server-side using `System.Drawing` into 120×120px transparent circular PNGs stored in `wwwroot/img/energy/`. Dragon, Fairy, and Colorless have no MEE equivalents and use styled letter-based fallbacks.

### Attack damage type
TCGDex returns the `damage` field as either a JSON number (e.g., `10`) or a JSON string (e.g., `"80+"`). A custom `NumberOrStringConverter` on the `Attack.Damage` property handles both, preventing deserialization failures on simple-damage cards.

### Set logo URL format
Set logo URLs from the API are base paths (e.g., `https://assets.tcgdex.net/en/me/me03/logo`). Appending `.webp` serves the image. This differs from card image URLs which require a quality suffix (`.../image/high.webp`).

---

## API Reference

**Base URL:** `https://api.tcgdex.net/v2/en`

| Endpoint | Returns |
|---|---|
| `GET /sets` | Array of all set briefs (id, name, logo, cardCount) |
| `GET /sets/{setId}` | Full set with cards array (id, localId, name, image) |
| `GET /sets/{setId}/{localId}` | Full card detail |

**Card image URLs:** `{image}/high.webp` (full quality) or `{image}/low.webp` (thumbnail)  
**Set logo URLs:** `{logo}.webp`

**Pricing:** Cardmarket (EUR). TCGPlayer data exists in the response schema but is `null` for all cards in the currently-tested sets.

---

## Known Limitations / Future Work

- **Authentication:** No auth currently. Before any deployment, Entra ID SSO should be added (org standard).
- **ADO work items:** This document and feature list should be imported into ADO as work items (not yet done).
- **Set filtering on home page:** No way to filter sets by "has cards in my collection" — would require a cross-reference query.
- **Search across all sets:** No global card search — must navigate to a set first.
- **Card image missing for some sets:** TCGDex doesn't have images for every card in every set; placeholder shown.
- **TCGPlayer pricing:** Always null from the API for tested sets. Cardmarket prices are EUR-only; conversion is a fixed approximate rate.
- **Energy icons — missing types:** Dragon, Fairy, Colorless use styled letter coins, not real card art.
- **Offline support:** App requires internet for TCGDex API calls (card data is not cached locally beyond the current page session).
- **No deck building / want list features** — strictly a "what do I own and where is it" tracker for now.

---

## Running Locally

```bash
cd C:\Users\clint.warner\source\lucalia\clints-card-shop
dotnet run
# Opens at http://localhost:5128
```

After changing static files in `wwwroot/`, hard-refresh the browser (`Ctrl+Shift+R`) to bust the cache.

If the DB schema changes, delete `collection.db` before restarting — EF Core will recreate it.
