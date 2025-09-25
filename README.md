
# SliceSharp — kontextbezogene Program‑Slices für .NET

**Kurzfassung:** SliceSharp erzeugt ausgehend von einem **Root** (API‑Action/Route oder `Datei.cs#Methode`) einen **präzisen, transitive Abhängigkeits‑Slice** deiner Lösung: nur die **relevanten** Controller/Handler → Services → Repositories → DTOs/Entities/Mappings usw. — **ohne** unnötigen Ballast.  
Der Slice wird als **`Slice.md`** exportiert (auf Wunsch **minifiziert**: *ohne* `using`s, *ohne* Namespaces, *ohne* Leerzeilen), plus **`graph.dot`** zur Visualisierung.

---

## Inhalt

- [Was macht SliceSharp?](#was-macht-slicesharp)
- [Wie funktioniert es intern?](#wie-funktioniert-es-intern)
- [Unterstützte Muster](#unterstützte-muster)
- [Installation & Build](#installation--build)
  - [.NET‑SDK/Tools](#net-sdktools)
  - [Build (Debug/Release)](#build-debugrelease)
  - [Veröffentlichen (Publish)](#veröffentlichen-publish)
  - [Als .NET‑Tool verwenden (optional)](#als-net-tool-verwenden-optional)
- [CLI‑Verwendung](#cli-verwendung)
  - [Root‑Formate](#root-formate)
  - [Parameter](#parameter)
  - [Beispiele](#beispiele)
  - [Exit Codes](#exit-codes)
- [Ausgabeformate](#ausgabeformate)
- [Troubleshooting](#troubleshooting)
- [Leistungs‑Tipps](#leistungs-tipps)
- [Roadmap & TODOs](#roadmap--todos)
- [Sicherheit & Datenschutz](#sicherheit--datenschutz)

---

## Was macht SliceSharp?

- **Findet** alle für einen konkreten Endpunkt/Screen relevanten Dateien und Typen.
- **Löst** gängige DI‑Registrierungen (Interface → Implementation) auf.
- **Berücksichtigt** AutoMapper‑Ziele und Profile sowie EF‑Core‑Entities/Configurations.
- **Exportiert** einen **LLM‑freundlichen** Kontext: `Slice.md` (wahlweise minifiziert) + `graph.dot`.

Ergebnis: schlanke, kontextspezifische Pakete — ideal fürs Debugging, Refactoring und für Coding‑Agents mit kleinem Kontextfenster.

---

## Wie funktioniert es intern?

1. **Solution laden** via `MSBuildWorkspace`.
2. **Root auflösen**  
   - `Datei.cs#Methode` *oder* `route:VERB:/pfad` (Controller‑Attribute & Tokens `[controller]`/`[action]` werden berücksichtigt).
3. **DI‑Registry scannen**  
   - `AddTransient/AddScoped/AddSingleton` (Generics & `typeof`), `ServiceDescriptor`, **Scrutor**‑Heuristik (`Scan(...).AddClasses().AsImplementedInterfaces()`).
4. **BFS‑Traversal (Roslyn/IOperation)**  
   - Aufrufe, `new`‑Erzeugungen, Signaturen, Konstruktions‑ und Property‑Typen, Interface‑Calls → **Implementation** (DI & `SymbolFinder.FindImplementationsAsync(...)`).
5. **Heuristiken**
   - **AutoMapper:** `Map<...>()`, `ProjectTo<...>()` → Zieltypen + **Profile** (im selben Projekt).
   - **EF Core:** `DbContext` → `DbSet<TEntity>`; verwendete `DbSet<T>` in Ausdrücken; **`IEntityTypeConfiguration<T>`** aus dem Projekt.
6. **Export**  
   - `Slice.md` (optional **minifiziert**: ohne `using`s, Namespaces, Leerzeilen), **Budget**-gesteuert.  
   - `graph.dot` (GraphViz).

---

## Unterstützte Muster

- **ASP.NET Core Controller**: `[Route]`, `[HttpGet]/[HttpPost]/...`, `[AcceptVerbs]`, Tokens `[controller]`/`[action]`.
- **Dependency Injection**: `AddTransient/AddScoped/AddSingleton`, `ServiceDescriptor`, **Scrutor** (Heuristik).
- **AutoMapper**: `Map<>()`, `ProjectTo<>()` + Profile (Klassen, die von `AutoMapper.Profile` ableiten).
- **Entity Framework Core**: `DbContext`/`DbSet<T>`, verwendete `DbSet<T>` in Methoden, `IEntityTypeConfiguration<T>`.

> **Noch nicht (geplant):** **Minimal‑APIs** (`app.MapGet/MapPost/...`), **.NET MAUI** XAML‑Bindings (CommunityToolkit.Mvvm), **AutoMapper**: exakte Map‑Selektion pro Typ‑Paar, **EF Core**: Navigationen/Query‑Shaping.

---

## Installation & Build

### .NET‑SDK/Tools

- **.NET SDK 8+** (9/10 kompatibel).  
- **MSBuild**: Wird automatisch gesucht. Falls Erkennung fehlschlägt:
  - Option `--msbuildPath` setzen (Ordner mit **MSBuild.dll** oder Pfad zu `msbuild.exe`),
  - oder Umgebungsvariable `SLICESHARP_MSBUILD_PATH` / `MSBUILD_EXE_PATH` setzen,
  - oder VS 2022 Build Tools installieren.

Optional:
- **GraphViz** (für `graph.dot` → PNG/SVG): `dot -Tpng graph.dot -o graph.png`.

### Build (Debug/Release)

```bash
dotnet restore
dotnet build -c Release
````

Ausführen aus der Projektmappe:

```bash
dotnet run --project src/SliceSharp.Cli/SliceSharp.Cli.csproj -- --sln <PfadZurSolution.sln> --root "<RootSpec>" --out <Ausgabeordner>
```

### Veröffentlichen (Publish)

**Framework‑abhängig** (kleine EXE, benötigt installiertes .NET):

```bash
dotnet publish src/SliceSharp.Cli/SliceSharp.Cli.csproj -c Release -o .\artifacts\win-x64 -r win-x64 --self-contained false
```

**Self‑contained, Single‑File, getrimmt** (größere EXE, kein .NET nötig):

```bash
dotnet publish src/SliceSharp.Cli/SliceSharp.Cli.csproj -c Release -r win-x64 -o .\artifacts\win-x64 ^
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial
```

**Weitere RIDs**: `linux-x64`, `osx-x64`, `osx-arm64` (Linux/macOS werden perspektivisch unterstützt; Entwicklung aktuell primär Windows).

### Als .NET‑Tool verwenden (optional)

> Du kannst SliceSharp als **lokales** oder **globales** Tool packen. Dafür muss das Projekt als Tool gepackt werden.

**csproj‑Ergänzung** *(Snippet — optional in `SliceSharp.Cli.csproj` einfügen)*:

```xml
<PropertyGroup>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>slicesharp</ToolCommandName>
  <PackageOutputPath>$(MSBuildProjectDirectory)\..\..\nupkg</PackageOutputPath>
</PropertyGroup>
```

**Packen & installieren (global):**

```bash
dotnet pack src/SliceSharp.Cli/SliceSharp.Cli.csproj -c Release
dotnet tool install --global slicesharp.cli --add-source .\nupkg --version <x.y.z>
slicesharp --help
```

**Lokal im Repo (Tool‑Manifest):**

```bash
dotnet new tool-manifest
dotnet tool install slicesharp.cli --add-source .\nupkg --version <x.y.z>
dotnet tool run slicesharp -- --sln <...> --root "<...>"
```

> **Hinweis:** Paketname/Version nach deinem Schema wählen. Alternativ kannst du ohne Tool‑Packaging einfach die veröffentlichte EXE verwenden.

---

## CLI‑Verwendung

### Root‑Formate

* **Datei & Methode:**
  `C:\pfad\zu\Controller.cs#ActionName`
  Optional mit grober Signatur: `#ActionName(int,string)` → bevorzugt passende Overload.

* **Route (Controller‑Attribute):**
  `route:GET:/api/orders/42`

### Parameter

| Parameter            | Typ/Default      | Beschreibung                                                                |
| -------------------- | ---------------- | --------------------------------------------------------------------------- |
| `--sln`              | **required**     | Pfad zur `.sln`.                                                            |
| `--root`             | **required**     | `path\to\File.cs#Method` **oder** `route:VERB:/pfad`.                       |
| `--out`              | `./slice-output` | Zielordner für `Slice.md` & `graph.dot`.                                    |
| `--budgetTokens`     | `32000`          | Token‑Budget (Export). \~Zeichen = `budgetTokens * avgCharsPerToken`.       |
| `--avgCharsPerToken` | `4.0`            | Heuristik: Zeichen pro Token.                                               |
| `--maxDepth`         | `20`             | Max. BFS‑Tiefe (Hops) ab Root.                                              |
| `--embedFullCode`    | `true`           | Volltexte einbetten (bis Budget), sonst Hinweis.                            |
| `--strip`            | `true`           | **Minifizierung** aktivieren (entfernt `using`s, Namespaces, Leerzeilen).   |
| `--msbuildPath`      | *(leer)*         | Optionaler Pfad zu MSBuild (Ordner mit **MSBuild.dll** oder `msbuild.exe`). |

### Beispiele

**Controller‑Datei & Methode:**

```bash
dotnet run --project src/SliceSharp.Cli/SliceSharp.Cli.csproj -- ^
  --sln C:\Users\JohannP\source\KPMG\KPMG.DA.WebApi\KPMG.DA.WebApi.sln ^
  --root "C:\Users\JohannP\source\KPMG\KPMG.DA.WebApi\KPMG.DA.WebApi\Controllers\JobsScopeBuilderController.cs#Start" ^
  --out C:\temp\start-slice --budgetTokens 32000 --maxDepth 20 --strip true
```

**Route (Controller‑Attribute):**

```bash
dotnet run --project src/SliceSharp.Cli/SliceSharp.Cli.csproj -- ^
  --sln C:\src\MyApp\MyApp.sln ^
  --root "route:GET:/api/orders/42" ^
  --out .\slices\orders
```

**MSBuild manuell angeben (falls Erkennung fehlschlägt):**

```bash
dotnet run --project src/SliceSharp.Cli/SliceSharp.Cli.csproj -- ^
  --sln C:\src\MyApp\MyApp.sln ^
  --root "route:POST:/api/jobs/{id}/start" ^
  --msbuildPath "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin"
```

### Exit Codes

* `0` → Erfolg
* `1` → Fehler/Exception
* `2` → Ungültige Parameter / Validierung fehlgeschlagen

---

## Ausgabeformate

* **`Slice.md`**

  * Header: Root, Budget, Tiefe, Datei‑Anzahl.
  * **Index** (relative Pfade).
  * **Files**: pro Datei ein ` ```csharp `‑Block.
  * Bei `--strip true`: **ohne** `using`s, **ohne** Namespaces, **ohne** Leerzeilen (Token‑sparend).
  * Bei Budget‑Überlauf: `// --- TRUNCATED DUE TO BUDGET ---`.

* **`graph.dot`** (GraphViz)

  * Rendern: `dot -Tpng graph.dot -o graph.png`

---

## Troubleshooting

**Nur sehr wenige Dateien im Slice?**

* Prüfe, ob in der Konsole `DI mappings: <Zahl>` erscheint (sollte >0 sein, falls DI statisch registriert wird).
* Nutzt ihr **eigene Extension‑Methoden** (z. B. `services.AddBusiness()`)? Der Scanner liest deren **Body**; dort müssen `AddScoped/Transient/...` auftauchen.
* Erhöhe testweise `--maxDepth` (z. B. 30–40) und/oder `--budgetTokens`.

**MSBuild wird nicht gefunden / Solution lädt nicht**

* `--msbuildPath` setzen oder `SLICESHARP_MSBUILD_PATH`/`MSBUILD_EXE_PATH` als Umgebungsvariable.
* VS 2022 Build Tools installieren (Developer Prompt).
* Alternative: `DOTNET_ROOT` korrekt gesetzt; ggf. .NET SDK reparieren.

**Root nicht gefunden**

* Bei `Datei.cs#Methode`: exakten Pfad/Methodennamen prüfen.
  Overloads? → Signatur grob angeben: `#Start(Guid,CancellationToken)`.
* Bei `route:`: Verb & Pfad exakt, Controller‑Attribute vorhanden? Tokens `[controller]`/`[action]` werden ersetzt.

**Zu aggressives Strippen?**

* `--strip false` verwenden, um Original‑Quelltext (mit `using`s/Namespace) zu exportieren.

---

## Leistungs‑Tipps

* **Hohe Tiefe/Budget** nur bei Bedarf; sonst wächst der Slice stark.
* **Minifizierung** (`--strip true`) reduziert Tokens drastisch.
* Große Lösungen: Erstlauf kann dauern (Roslyn/Analyzers warm‑up).

---

## Roadmap & TODOs

**Kurzfristig**

* **Minimal‑APIs**: `app.MapGet/MapPost/...` → Handler‑Delegates/Methoden auflösen (Root & Graph).
* **AutoMapper (präziser)**: nur Profile/Maps, die wirklich die im Slice vorkommenden Typ‑Paare betreffen.
* **EF Core (präziser)**: Navigationen & Query‑Shaping heuristisch (nur tatsächlich berührte Entitäten).

**Mittelfristig**

* **.NET MAUI**: XAML‑Bindings (CommunityToolkit.Mvvm) → ViewModels/Commands/Services.
* **Ranking/Scoring**: gewichtet nach Nähe zum Root; harte Budgetkappung mit „must‑have“ Ebenen.
* **Cache/Incremental**: Symbol‑Index & DI‑Map persistieren (schnellere Folgeläufe).
* **VSIX**: Kontextmenü „Slice erstellen“, Token‑Schätzung, interaktive Auswahl.
* **CI‑Integration**: GitHub Action/Azure DevOps Task (PR‑bezogene Slices).

---

## Sicherheit & Datenschutz

* SliceSharp liest **lokal** aus deiner Solution; es werden keine Daten übertragen.
* Prüfe vor dem Teilen von `Slice.md`, ob sensible Informationen enthalten sind.
  Mit `--strip true` verschwinden zumindest `using`s/Namespaces/Leerzeilen – **Inhalt** bleibt jedoch der gleiche.

---

## Lizenz & Beiträge

* (Projektintern anpassen)
* Issues/Feature‑Requests: gerne mit **konkreten Beispielen** (Route/Datei, erwartete Dateien).

---

## Changelog (Auszug)

* **Aktuell**: Minifizierter Export (`--strip`), AutoMapper‑ & EF‑Core‑Heuristiken, verbesserter DI‑Scan, `SymbolFinder`‑Fallbacks, Controller‑Route‑Resolver.

```
