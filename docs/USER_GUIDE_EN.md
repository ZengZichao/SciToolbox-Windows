# SciToolbox User Guide

- [中文](使用指南.md) · [English](USER_GUIDE_EN.md)

---

## Contents

1. [Interface tour](#1-interface-tour)
2. [Looking up a record](#2-looking-up-a-record)
3. [Smart routing and cross-database links](#3-smart-routing-and-cross-database-links)
4. [Search across all databases](#4-search-across-all-databases)
5. [Reading the detail pane](#5-reading-the-detail-pane)
6. [Favorites, history and collections](#6-favorites-history-and-collections)
7. [Batch operations and comparison](#7-batch-operations-and-comparison)
8. [Export and citation formats](#8-export-and-citation-formats)
9. [What each database is good at](#9-what-each-database-is-good-at)
10. [Settings reference](#10-settings-reference)
11. [Keyboard shortcuts](#11-keyboard-shortcuts)
12. [Where your data lives](#12-where-your-data-lives)
13. [Troubleshooting](#13-troubleshooting)

---

## 1. Interface tour

The window has three panes plus a top toolbar.

**Top toolbar** (left to right)

| Element | Purpose |
| --- | --- |
| Sidebar toggle | Show / hide the navigation pane — same as `Ctrl` + `\` |
| Back | Return to the previously viewed tool; handy after cross-database jumps |
| Breadcrumb | The last three steps of your navigation path |
| Cache / throttle badge | Appears when a result came from the local cache, or when a request is queued |
| Search (top right) | Open the command palette — same as `Ctrl` + `K` |
| List icon (top right) | Open the shortcut reference |

**Left pane · navigation**: Home, the 15 databases grouped into five categories, Favorites & History, Collections, and Settings. Category headers collapse, and the collapsed state is remembered.

**Middle pane · search and results**: title, search box, result list.

**Right pane · detail**: expands the record you select; shows guidance when nothing is selected.

All pane dividers are draggable.

---

## 2. Looking up a record

1. Pick a database on the left (for example **UniProt**).
2. Type a keyword or accession, then press Enter or click the magnifier.
3. Each row shows a title, subtitle, accession badge and short metadata.
4. Click a row to load the full record in the right pane.
5. With many hits, **Load more** appends the next page without re-requesting what you already have.

**Row-level actions**

- The star on each row favorites or unfavorites without opening the record.
- `Ctrl` + click several rows to build a multi-selection.
- Use `↑` `↓` to move the highlight and `Enter` to open it.

**Filtering loaded results**: once results appear, the local filter box matches text **within the loaded set only** — it never re-queries the API. Clearing it restores the full list.

---

## 3. Smart routing and cross-database links

The **Quick start** box on the Home page is a universal entry point: paste an identifier, click "Identify and open", and SciToolbox works out which database it belongs to.

Recognised shapes include:

| Input | Destination |
| --- | --- |
| `P12345`, `Q9Y697` | UniProt |
| `1ABC`, `4HHB` | RCSB PDB |
| `TP53`, `BRCA2` | Gene symbol → NCBI Gene / Ensembl |
| `10.1000/xyz…` | DOI → Europe PMC |
| `GO:0006915` | QuickGO |
| `PF00069`, `IPR000719` | Pfam / InterPro |
| `ENSG00000141510` | Ensembl |
| `hsa04110`, `C00001` | KEGG |
| Numeric TaxID | NCBI Taxonomy |

**When the input is ambiguous** (an identifier that several databases could claim), a dialog lists each candidate with its target database, the query it would run, the reason it matched, and a confidence percentage. Click the one you want.

**Cross-database links inside a record**: many fields are themselves links — PDB IDs inside a UniProt entry, UniProt accessions inside a PDB entry. Clicking one switches database and runs the search for you; **Back** in the toolbar returns you.

---

## 4. Search across all databases

The **Search all databases** box on the Home page queries all 15 sources **in parallel** and groups the hits by category, showing each source's hit count and its top five records.

- A failure or timeout in one source never blocks the others; failed sources are labelled with the reason.
- The **Open** button on each source carries the keyword into that database for a full search.
- The current query is shown at the top, with **Retry** to re-run everything.

Useful when you only know a gene name, protein name or topic and are unsure which resource to start in.

---

## 5. Reading the detail pane

When a record is selected, the right pane shows, in blocks:

- **Colour bar** — the category the record belongs to.
- **Header** — title, subtitle, and metadata chips.
- **Action icons** — favorite, add to collection, add to comparison, open on the source website.
- **Structure / pathway preview image** (PDB, AlphaFold and KEGG records that provide one).
- **Grouped field table** — sections with titles, then key–value rows. Values are selectable, and copyable fields have a copy button.
- **Long-form text blocks** — abstracts, comments, sequence descriptions.
- **Action buttons** — exports available for that record (copy FASTA, export BibTeX, and so on).
- **Cross-links** — related records you can jump to.

Detail browsing keeps a history stack; step through it with `Ctrl` + `[` / `Ctrl` + `]` or the arrows at the top of the pane.

Failures are never silent — you get the actual reason plus a **Retry** button.

---

## 6. Favorites, history and collections

**Favorites**: the star on the detail pane or on any result row. Add notes, filter, export as text or JSON, and delete individual entries with an **Undo** offer.

**History**: every successful search is recorded. **Re-run** reuses a query; individual entries are undoable; or clear everything.

**Collections**: group related records by project.

1. Open Collections on the left and click **New collection**.
2. From any detail view or result row choose **Add to collection**, then pick a collection or create one inline.
3. Open a collection to list, annotate, remove, rename or delete it.
4. Export a collection as **CSV** (for spreadsheets) or **JSON** (full structural backup).
5. **Batch verify** re-fetches each entry to check the identifiers are still valid, reporting progress as it goes.

---

## 7. Batch operations and comparison

**Batch**: click **Enter multi-select** above the results, or just `Ctrl` + click rows. The toolbar then offers:

| Action | Result |
| --- | --- |
| Copy accessions | Selected accessions, one per line, on the clipboard |
| Batch favorite | Favorite everything selected at once |
| Add to collection | Archive the selection into a collection |
| Export table | Write a CSV (accession / title / subtitle / other info) |

**Compare**: add records to the comparison tray (up to 4), then use **View comparison** for a side-by-side layout with aligned fields — useful for comparing the same entity across databases.

---

## 8. Export and citation formats

| Content | Format | Available from |
| --- | --- | --- |
| Protein / gene / sequence | FASTA | UniProt, Ensembl, AlphaFold |
| Literature records | BibTeX, RIS | PubMed, Europe PMC |
| Search results / collections | CSV | Batch export, collection export |
| Favorites / all local data | JSON | Settings page and Favorites page |

Action buttons copy to the clipboard by default; ones labelled as exports open a save dialog. BibTeX and RIS import directly into Zotero, EndNote and Mendeley.

---

## 9. What each database is good at

| Tool | Best queried with | Notable |
| --- | --- | --- |
| UniProt | accession, gene name, species + keyword | FASTA copy, structure cross-links |
| RCSB PDB | 4-character PDB ID | Structure preview, method and resolution |
| AlphaFold | UniProt accession | Predicted structure image and confidence |
| PubMed | PubMed query syntax | **Year-range filter**, BibTeX / RIS |
| Europe PMC | PMID, DOI, keyword | Open-access flag, BibTeX / RIS |
| NCBI Gene | gene name / GeneID | Dense cross-database links |
| NCBI Taxonomy | scientific name or TaxID | Full lineage chain |
| Ensembl | gene symbol / ENSG | FASTA, coordinates |
| KEGG | `hsa04110`, compound IDs | Pathway image |
| QuickGO | `GO:0006915` or term text | Ontology hierarchy |
| Pfam / InterPro | `PF00069`, `IPR…` | Family and domain annotation |
| GBIF | scientific or common name | Classification and occurrence records |
| BacDive | strain name / taxonomy ID | Microbial strain data |
| MGnify | study accession | Metagenomic studies |
| GTDB Official | taxon name | Pre-loaded list — no query needed to start |

---

## 10. Settings reference

| Group | Option | Notes |
| --- | --- | --- |
| Appearance | Theme | Follow system / Light / Dark. In "follow system" the app reacts live to OS theme changes |
| Appearance | Sidebar density | Compact / Normal / Spacious — row height and font size |
| Appearance | Language | 中文 / English, applied immediately |
| Cache | Enable result cache | Turn off to always hit the network; cache can be cleared independently |
| Cache | Keyboard shortcuts | Open the shortcut reference |
| Data | Export all local data | Favorites + history + collections bundled as JSON |
| Data | Clear favorites / history | Confirmed before running |
| Data | Reset app | Wipes all local data including cache; double confirmation |

---

## 11. Keyboard shortcuts

| Keys | Action |
| --- | --- |
| `Ctrl` + `K` | Command palette |
| `Ctrl` + `F` | Focus the search box |
| `Ctrl` + `,` | Open Settings |
| `Ctrl` + `/` | Shortcut reference |
| `Ctrl` + `\` | Show / hide the sidebar |
| `Ctrl` + `[` | Previous detail record |
| `Ctrl` + `]` | Next detail record |
| `↑` `↓` | Move the highlight in a result list |
| `Home` `End` | Jump to first / last row |
| `Enter` | Open the highlighted row; run the search from the search box |
| `Ctrl` + click | Multi-select result rows |
| `Ctrl` + `Enter` | Save the note |
| `Esc` | Dismiss the open dialog |

---

## 12. Where your data lives

Everything stays on this machine:

```
%LOCALAPPDATA%\SciToolbox\
├── prefs.json     # settings + search history + favorites + collections
├── Cache\         # API response cache
├── crash.log      # runtime errors (only created if something failed)
└── binding.log    # UI binding diagnostics (only with --trace)
```

- To back up or move machines: copy `prefs.json`, or use **Export all local data**.
- To erase everything: use **Reset app** in Settings, or delete the folder.

The app writes nothing to the registry and sends nothing anywhere except the official APIs of the databases it queries.

---

## 13. Troubleshooting

**No results**
Check whether the offline banner is showing at the top. If a source genuinely has no match, the results area says so and offers a one-click search across all databases.

**"Too many requests"**
This is upstream rate limiting (NCBI in particular). The app queues and backs off automatically — wait a moment rather than clicking again.

**The detail pane looks empty**
It now reports the actual error with a **Retry** button. If the API simply returned nothing, that identifier does not exist in this database — try a cross-link or another source.

**The app didn't follow my OS theme change**
Set the theme to **Follow system**; the app listens for OS preference changes at runtime.

**Mixed-language interface text**
Switching language recalculates all UI text immediately. Any remaining text in the other language is content returned by the database API itself, not interface copy.

**Lost my data**
Check that `%LOCALAPPDATA%\SciToolbox\prefs.json` still exists. If the folder was deleted manually the data cannot be recovered — export regularly as a backup.
