# SciToolbox

> A native **WPF / C# / .NET 8** desktop app that brings **15 public academic databases** behind a single search box.
> No sign-in, no account, no telemetry — all local data stays on your machine.

[中文说明](README.md) · [User guide](docs/USER_GUIDE_EN.md) · [License](LICENSE)

---

## What it solves

Answering one research question usually means bouncing between UniProt, PDB, PubMed, NCBI, Ensembl, KEGG, GO, GBIF and more — each with its own interface, filters and export format.

SciToolbox wraps the public APIs of those databases behind one consistent UI: one search model, one detail layout, one cross-database link graph, one local place for favorites and exports. No account required, and nothing about you is uploaded.

## Capabilities

| Capability | Details |
| --- | --- |
| **Three-pane workspace** | Sidebar (tool navigation) → middle (search + results) → right (detail). Panes are resizable; the sidebar collapses entirely |
| **15 data sources** | Grouped into Taxonomy, Genes & Sequences, Proteins & Structures, Functions & Pathways, Literature — each with its own colour signature |
| **Smart accession routing** | Paste `P12345`, `1ABC`, `TP53`, a DOI, `GO:xxxx`, `ENSxxxx`, `PFxxxx` — the type is detected and you are taken straight there. Ambiguous input opens a picker with confidence scores |
| **Search across all databases** | One keyword queries all 15 sources in parallel, grouped by category; a failure in one source never blocks the others |
| **Cross-database links** | Related records in a detail view jump to the right database; the detail pane keeps a browse history you can step through |
| **Favorites / history / collections** | Fully local. Notes, filtering, undoable deletion; collections group entries by project and export to CSV / JSON |
| **Batch operations** | Select multiple results, then copy accessions, favorite, add to a collection, or export a table |
| **Side-by-side comparison** | Add entries to the compare tray and view aligned fields together |
| **Citation export** | BibTeX / RIS / FASTA — copy to clipboard or write to a file, compatible with Zotero, EndNote and Mendeley |
| **Network friendly** | Offline detection with pre-flight blocking, on-disk response cache, exponential-backoff retries, NCBI request throttling with a queue indicator |
| **Bilingual UI** | Switch between Chinese and English in Settings; applies immediately |
| **Light / dark theme** | Follow the system, and react live when the OS theme changes |

See the **[user guide](docs/USER_GUIDE_EN.md)** for full details.

## Data sources

| Category | Tool | Endpoint |
| --- | --- | --- |
| Taxonomy | GTDB Official | gtdb-api.ecogenomic.org |
| Taxonomy | NCBI Taxonomy | eutils.ncbi.nlm.nih.gov |
| Taxonomy | BacDive | api.bacdive.dsmz.de |
| Taxonomy | MGnify | www.ebi.ac.uk/metagenomics |
| Taxonomy | GBIF | api.gbif.org |
| Genes & Sequences | NCBI Gene | eutils.ncbi.nlm.nih.gov |
| Genes & Sequences | Ensembl | rest.ensembl.org |
| Proteins & Structures | UniProt | rest.uniprot.org |
| Proteins & Structures | RCSB PDB | data.rcsb.org |
| Proteins & Structures | AlphaFold | alphafold.ebi.ac.uk |
| Proteins & Structures | Pfam / InterPro | www.ebi.ac.uk/interpro |
| Functions & Pathways | KEGG | rest.kegg.jp |
| Functions & Pathways | QuickGO | www.ebi.ac.uk/QuickGO |
| Literature | PubMed | eutils.ncbi.nlm.nih.gov |
| Literature | Europe PMC | www.ebi.ac.uk/europepmc |

Provenance is always shown in-app, and each API's terms of use and rate limits are respected.

## Requirements

- Windows 10 / 11 (x64)
- The self-contained build (`publish\SciToolbox.exe`) needs **no .NET runtime installed**
- Building from source requires the .NET 8 SDK

## Getting started

Download the single-file self-contained `SciToolbox.exe` from the releases page — no installer, just run it.

From source:

```powershell
git clone <repo-url>
cd SciToolbox

# run from source
dotnet run --project src\SciToolbox

# Release build
.\build.ps1 -Release

# self-contained single-file exe -> publish\SciToolbox.exe
.\build.ps1 -Publish
```

## Keyboard shortcuts

| Keys | Action |
| --- | --- |
| `Ctrl` + `K` | Command palette; jump to a tool by name, `uniprot:P12345` syntax supported |
| `Ctrl` + `F` | Focus the current search box |
| `Ctrl` + `,` | Open Settings |
| `Ctrl` + `/` | Shortcut reference |
| `Ctrl` + `\` | Show / hide the sidebar |
| `Ctrl` + `[` / `]` | Previous / next detail record |
| `↑` / `↓` / `Home` / `End` | Move the highlight in a result list |
| `Enter` | Open the highlighted result |
| `Ctrl` + click | Multi-select result rows |
| `Ctrl` + `Enter` | Save the note |
| `Esc` | Dismiss the open dialog |

## Project layout

```
SciToolbox/
├── SciToolbox.sln
├── build.ps1                    # build / publish script
├── README.md                    # Chinese readme
├── README_EN.md                 # English readme (this file)
├── LICENSE                      # GPL-3.0
├── docs/
│   ├── 使用指南.md              # Chinese user guide
│   └── USER_GUIDE_EN.md         # English user guide
└── src/SciToolbox/
    ├── App.xaml(.cs)            # entry point, theming, global error handling, CLI diagnostics
    ├── MainWindow.xaml(.cs)     # three-pane shell, toolbar, shortcuts, overlays
    ├── Resources/AppIcon.ico    # application icon (exe / taskbar / in-app)
    ├── Core/                    # UI-agnostic core
    │   ├── Models.cs  Json.cs  Errors.cs  Log.cs
    │   ├── ApiClient.cs  ResponseCache.cs  Prefs.cs
    │   ├── Theme.cs  Localization.cs  DomainMaps.cs  RelativeTime.cs
    │   ├── ProviderHelpers.cs  ToolRegistry.cs  AccessionRouter.cs
    │   ├── Stores.cs  CollectionStore.cs  ComparisonStore.cs
    │   └── Util.cs              # clipboard / toast / export / network monitor
    ├── Providers/               # 15 data source adapters
    ├── ViewModels/              # MVVM view models
    └── Views/                   # styles, templates, converters, overlay controls
```

## Development checks

After changing UI code:

```powershell
.\build.ps1 -Release
Remove-Item "$env:LOCALAPPDATA\SciToolbox\binding.log","$env:LOCALAPPDATA\SciToolbox\crash.log" -ErrorAction SilentlyContinue
publish\SciToolbox.exe --smoke --trace
```

`--smoke` walks every page and seeds sample data so each view template is instantiated; `--trace` writes binding and resource errors to `%LOCALAPPDATA%\SciToolbox\binding.log`. The run is clean only if neither log file appears.

Other diagnostic flags:

| Flag | Purpose |
| --- | --- |
| `--demo <toolId> <query>` | Navigate, search and open the first detail automatically — useful for reproducible checks |
| `--selftest <toolId> <query>` | Headless search + detail run, results written to `selftest.json` |

## Data and privacy

- No login, no account system, no collection or upload of personal data
- Search history, favorites and collections live in `%LOCALAPPDATA%\SciToolbox\prefs.json` on your machine; export or wipe them at any time
- The cache stores request → response pairs only, with no user or device identifier
- Every network call goes directly to the official HTTPS API of the public database — there is no intermediary server

## License

Licensed under **GPL-3.0**. See [LICENSE](LICENSE).
