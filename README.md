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

### Einstellungen (Umgebungsvariablen)

Keine davon ist Pflicht; ohne die `MAT*`-Variablen verhält sich MatPlay wie bisher.

| Variable | Standard | Bedeutung |
|---|---|---|
| `MATPLAY_DATA` | `/data` (Container), lokal `appdata/` | Datenverzeichnis: `db/`, `config/`, `keys/` |
| `APP_VERSION` | `local-<datum>` | Angezeigte Version (setzt der CI-Build) |
| `MAT_API_KEY` | – | Schlüssel für den geschützten Teil der [Plattform-API](#plattform-api) (Header `X-Api-Key`). Nicht gesetzt = dort nur Tokens von MatIdentity |
| `MATIDENTITY_AUTHORITY` | – | Öffentliche Adresse von [MatIdentity](#anmeldung-über-matidentity) (`https://id.example.com`). Gesetzt = Anmeldung über MatIdentity |
| `MATIDENTITY_CLIENT_ID` | `matplay` | Der Client von MatPlay in MatIdentity (auch die Audience der API-Tokens) |
| `MATIDENTITY_CLIENT_SECRET` | – | Sein Secret |
| `MATIDENTITY_INTERNAL_URL` | – | Wo MatPlay selbst MatIdentity erreicht, wenn das von der öffentlichen Adresse abweicht (in Docker z. B. `http://matidentity:2300`): Discovery, Schlüssel und Token-Abrufe gehen dorthin, Browser und erwarteter Issuer bleiben öffentlich |

### Plattform-API

MatPlay erfüllt den Plattform-Vertrag der Mat-Apps (MatDesign `docs/PLATFORM.md`), über das gemeinsame Paket
**MatPlatform** (Kopie unter `src/MatPlay/MatPlatform/`, nicht von Hand ändern; neu kopieren mit
`node tools/matplatform-copy.js <MatPlay>/src/MatPlay` im MatDesign-Repo). Beschrieben wird MatPlay in
`Platform/PlayPlatform.cs`; so zeigt Mattiverse (oder jede andere App) MatPlay an, ohne es vorher zu kennen.

| Endpunkt | Zweck | Zugriff |
|---|---|---|
| `GET /api` | die Definition: Seiten, Kacheln, Einstellungen, Aktionen, Rollen | anonym |
| `GET /api/health` | `ok` / `degraded` / `down` (Datenbank, aktiver Admin); 503 nur bei `down` – der Docker-`HEALTHCHECK` ruft das auf | anonym |
| `GET /api/state`, `GET /api/state/{key}` | die Werte der Kacheln | Schlüssel oder Token |
| `GET`/`PUT /api/configure` | die wenigen Einstellungen | Schlüssel oder Token |
| `POST /api/actions/{id}` | eine Aktion ausführen | Schlüssel oder Token |

- **Kacheln:** Spiele, laufende Spiele, Spiele der letzten 30 Tage, Spielerprofile, Benutzerkonten,
  meistgespieltes Spiel, letzter Spieleabend (letzte Änderung an einem Spiel).
- **Einstellungen:** Selbst-Registrierung erlauben, Länge der automatischen Kürzel (1–4) – dieselben Werte wie
  unter Administration → Einstellungen. Änderungen landen mit dem Aufrufer im Log.
- **Aktionen:** Benutzer verwalten und Alle Einstellungen (Links), Abgelaufene Sitzungen löschen.
- **Rollen:** `admin` (Administrator), `user` (Benutzer).

*Schlüssel* ist der Header `X-Api-Key` mit dem Wert von `MAT_API_KEY` (nicht gesetzt = aus); *Token* ist ein
Bearer-Token von MatIdentity für die Audience `MATIDENTITY_CLIENT_ID`. Texte folgen `Accept-Language` (`de`, `en`).
Die Spiel-API unter `/api/play/{token}` (Share-Links) bleibt davon unberührt.

```bash
curl http://localhost:4664/api
curl -H "X-Api-Key: $MAT_API_KEY" http://localhost:4664/api/state
```

### Anmeldung über MatIdentity

Optional: ohne `MATIDENTITY_AUTHORITY` ändert sich nichts. Mit der Variable:

- In MatIdentity einen Client für MatPlay anlegen (Mattiverse erledigt das beim Installieren) mit der
  Weiterleitungsadresse `https://<matplay>/signin-oidc` und der Abmelde-Adresse `https://<matplay>/signout-callback-oidc`;
  Id und Secret als `MATIDENTITY_CLIENT_ID` / `MATIDENTITY_CLIENT_SECRET` übergeben. Angemeldet wird mit OpenID Connect
  (Authorization Code + PKCE).
- „Anmelden“ führt direkt zu MatIdentity. Das lokale Formular bleibt unter `/account/login?local=1` erreichbar
  (der Weg für den Admin, wenn MatIdentity nicht läuft); ist MatIdentity nicht erreichbar, zeigt die Seite es mit Hinweis.
- Die Person wird über `sub` einem lokalen Benutzer zugeordnet (Spalte `User.ExternalSubject`); gibt es keinen, wird bei der
  ersten Anmeldung ein neuer angelegt (ohne lokales Passwort).
- **Ein vorhandenes Konto wird nur mit Beweis verknüpft.** Den Benutzernamen kann sich bei der Registrierung jeder aussuchen, auch
  die Adresse eines anderen; deshalb wird ein Konto, dessen Benutzername die E-Mail-Adresse der Person ist, nicht über die Adresse
  allein verknüpft (und kein zweites angelegt). MatPlay merkt sich die Verknüpfung 10 Minuten (geschütztes Cookie) und zeigt die
  lokale Anmeldung (`?local=1&link=1`): „Hier gibt es schon ein Konto mit der Adresse …“. Meldet sich genau dieses Konto mit seinem
  Passwort an, bekommt es den `sub` (die Rolle folgt dem Claim, die Sitzung behält das ID-Token, das Log schreibt die Verknüpfung);
  meldet sich ein anderes Konto an oder bricht die Person ab, wird nichts verknüpft. Ein passendes Konto ohne Passwort kann das nicht
  beweisen: dann wird daneben ein neuer Benutzer angelegt, wie bei einem Konto, das schon mit einer anderen Person verknüpft ist.
- Bringt MatIdentity den Claim `role` mit (`admin` oder `user`), folgt die Rolle ihm bei jeder Anmeldung (der letzte Admin
  wird nicht herabgestuft); ohne Claim verwaltet MatPlay die Rollen selbst wie bisher.
- „Abmelden“ beendet die Sitzung in MatPlay und bei MatIdentity.
- Spielen als Gast und die Share-Links funktionieren weiterhin ohne Anmeldung.

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
| Auth | Lokale Anmeldung, DB-Sessions (Token); optional MatIdentity (OpenID Connect) |
| Plattform | MatPlatform 1.0.0 (Kopie aus MatDesign): `/api`, `/api/health`, `/api/state`, `/api/configure`, `/api/actions` |
| Tests | xunit + WebApplicationFactory (`dotnet test MatPlay.slnx`), laufen in GitHub Actions vor dem Docker-Build |
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
├── Data/            # EF-Core-Entities + DbContext (PascalCase-Tabellen, Audit-Felder), Schema-Upgrade (DbInitializer)
├── Services/        # Auth/Session, GameService, Modul-Registry, JSON-Config, Kennzahlen, MatIdentity-Benutzer
├── Platform/        # Plattform-Vertrag: PlayPlatform (Definition + Werte), PlayIdentityUsers
├── MatPlatform/     # MatPlatform-Paket (Kopie aus MatDesign, nicht von Hand ändern)
├── Pages/           # Razor Pages (Games, Play, Account, Admin)
│   └── Shared/Controls/   # Wiederverwendbare Controls: Toolbar, Pagination, Tabs
└── wwwroot/
    ├── matui/       # MatUi-Toolkit (Kopie aus MatDesign, nicht von Hand ändern)
    ├── css/app.css  # nur App-Spezifisches (Spielkarten, Zettel), Präfix mp-, nur --mat-* Tokens
    └── js/          # Play-Module, Seiten-Skripte, PWA (Manifest, Service Worker, Icons)
```

Tests liegen unter `tests/MatPlay.Tests/` (Plattform-API, JSON-Pins der Definition in `Golden/` – neu schreiben nur
absichtlich mit `MATPLAY_WRITE_GOLDEN=1` –, Anmeldung mit und ohne MatIdentity, Schema-Upgrade): `dotnet test MatPlay.slnx`.

### Neues Spielmodul hinzufügen

1. Modul in `Services/GameModules.cs` registrieren (Key, Name, Icon, Partial)
2. Play-Partial unter `Pages/Play/Modules/_MeinModul.cshtml` anlegen
3. Client-Logik als `wwwroot/js/play-meinmodul.js` (nutzt `MatPlayCore` für API & Live-Sync)
4. Optional ein Preset für die Startseite ergänzen
