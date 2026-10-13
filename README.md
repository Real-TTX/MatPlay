# 🎮 MatPlay

Der Helfer für den Spieleabend: Punkte zählen für Brett- und Kartenspiele –
auf einem Handy oder per geteiltem Link auf mehreren Geräten gleichzeitig.
Als PWA installierbar, mit Wake-Lock („Display bleibt an") am Spieltisch.

## Features

- 🎯 **Punktezähler** – generisch konfigurierbar (Startpunkte, Schrittweite, Ziel, Runden)
- 🃏 **Presets** – z.B. „20 Ab", „Phase 10 (Punkte)", „Frantic" und „Flip 7" als vorkonfigurierte Zähler
- 🎲 **Qwixx** – digitaler Zettel mit Farbreihen, Schlössern und Fehlwürfen;
  inkl. „Qwixx gemixxt"-Varianten (wilde Zahlen / Farbsegmente), pro Spiel frisch ausgewürfelt
- 🎰 **Kniffel** – kompletter Block (oberer/unterer Teil), Bonus wird automatisch gerechnet
- 🧙 **Wizard** – Ansage vs. Stiche pro Runde, Punkte werden automatisch berechnet
- 🃏 **Viele weitere Presets** – Skyjo, 6 nimmt!, Uno, Rommé, Canasta, Doppelkopf, Skat,
  Schocken, Carcassonne und Darts 501 (Abzieh-Modus), jeweils mit Kurzregeln im Spiel
- ⚔️ **Munchkin & Munchkin Quest** – Level, Boni und Kampfkraft, optional mit Lebenspunkten
- 🔗 **Link-Freigabe** – jedes Spiel hat einen Share-Link, alle Geräte synchronisieren live
- 🔍 **Spielkatalog** – Startseite mit Suche und Paging, bereit für viele weitere Spiele
- ⭐ **Favoriten** – angemeldete Benutzer markieren Lieblingsspiele, die zuerst erscheinen
- 🧑‍🤝‍🧑 **Spielerprofile** – Mitspieler werden (abschaltbar) automatisch gespeichert, sind beim
  neuen Spiel als Vorauswahl antippbar und zeigen ihre Spiel-Historie; verwaltbar unter „Spieler".
  Jeder Spieler erhält automatisch ein eindeutiges Kürzel (Länge global einstellbar, Standard 2 –
  „Matthias" = Ma, „Manuel" = Mn) und eine Farbe; beides manuell änderbar
- ⚙️ **Globale Einstellungen** – Administration → Einstellungen (Registrierung, Kürzel-Länge)
- 👤 **Anonym spielbar** – Spiele erstellen ohne Konto; lokale Anmeldung mit Rollen (Admin, User, Anonym)
- 📱 **PWA** – installierbar, Offline-Shell, Screen Wake Lock (Android & iOS ≥ 16.4)
- 🌗 **Mat-Design** – nutzt das MatUi-Toolkit der Mat-Apps (System / Hell / Dunkel, flach, barrierearm)

## Schnellstart

```bash
# Release-Stack (Port 4664)
docker compose up -d --build

# Dev-Stack (zusätzlich sqlite-web als DB-Admin auf Port 4665)
docker compose -f docker-compose.dev.yml up -d --build
```

- App: http://localhost:4664
- DB-Admin (nur Dev): http://localhost:4665 – *sqlite-web statt pgAdmin, da SQLite als Datenbank dient*
- Erster Login: **admin / admin** – beim ersten Login wird automatisch eine
  Passwort-Änderung erzwungen, danach können weitere Konten angelegt werden.

Alle Daten (SQLite-DB, JSON-Configs) liegen im Docker-Volume unter `/data` –
Sessions und Spielstände überleben Container-Restarts.

## Live-Reload / Testen

Einfach den Container neu bauen und den Stack neu deployen:

```bash
docker compose -f docker-compose.dev.yml up -d --build
```

## Design (MatUi)

MatPlay folgt dem Mat-Design-System (Projekt *MatDesign*, Regelwerk + Toolkit). Das Toolkit
(`matui.css`, `matui.js`, `matui.boot.js`, `matui-icons.svg`) liegt als **Kopie** unter
`wwwroot/matui/` (`CODE-03`: kopieren, nicht verlinken) und wird per Neu-Kopieren aktualisiert;
die Version steht in der ersten Zeile von `matui.css` – `node tools/matui-check.js <MatPlay-Ordner>`
im MatDesign-Repo zeigt, ob die Kopie aktuell ist. Die Akzentfarbe von MatPlay (`#00819b`) ist dort
registriert (`<html data-app="matplay">`).

Konventionen: Shell, Menü, Toolbar, Pager, Tabellen, Formulare, Dialoge, Picker und Theme-Umschalter
kommen aus dem Toolkit; eigene Klassen heißen `mp-*` und nutzen nur `--mat-*`-Tokens. Keine
Inline-Styles/-Skripte, keine `confirm`/`alert`/`prompt` (stattdessen `data-confirm`, `MatUi.confirm`,
`MatUi.toast`). Icons kommen aus dem Sprite; die Emojis der Spielarten sind deren „Cover“.

## Stack

| Komponente | Technologie |
|---|---|
| Backend | C# / ASP.NET Core Razor Pages (.NET 10) |
| Datenbank | SQLite (EF Core) – Logik; JSON – Configs |
| Auth | Lokale Anmeldung, DB-Sessions (Token) |
| Deployment | Docker, docker-compose (dev/release), GitHub Actions → GHCR |

## Versionierung

| Kanal | Schema | Beispiel |
|---|---|---|
| Release (`main`) | `<major>.<minor>.<buildnr>-<datum>` | `1.0.42-20260904` |
| Development (`dev`) | `nightly-<buildnr>-<datum>` | `nightly-42-20260904` |
| Lokal | `local-<datum>` | `local-20260904` |

Die Buildnummer ist die fortlaufende GitHub-Actions-Run-Nummer. Die laufende
Version wird unten links im Menü angezeigt.

## Projektstruktur

```
src/MatPlay/
├── Data/            # EF-Core-Entities + DbContext (PascalCase-Tabellen, Audit-Felder)
├── Services/        # Auth/Session, GameService, Modul-Registry, JSON-Config
├── Pages/           # Razor Pages (Games, Play, Account, Admin)
│   └── Shared/Controls/   # Wiederverwendbare Controls: Toolbar, Pagination, Tabs
└── wwwroot/
    ├── matui/       # MatUi-Toolkit (Kopie aus MatDesign, nicht von Hand ändern)
    ├── css/app.css  # nur App-Spezifisches (Spielkarten, Zettel), Präfix mp-, nur --mat-* Tokens
    └── js/          # Play-Module, Seiten-Skripte, PWA (Manifest, Service Worker, Icons)
```

### Neues Spielmodul hinzufügen

1. Modul in `Services/GameModules.cs` registrieren (Key, Name, Icon, Partial)
2. Play-Partial unter `Pages/Play/Modules/_MeinModul.cshtml` anlegen
3. Client-Logik als `wwwroot/js/play-meinmodul.js` (nutzt `MatPlayCore` für API & Live-Sync)
4. Optional ein Preset für die Startseite ergänzen
