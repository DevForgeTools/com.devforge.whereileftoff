# Changelog
All notable changes to **Where I Left Off** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2025-09-24
### Changed
- **README** — Consolidated into a single file inside the package; localized variants (`README.es.md`, `README.en.md`) were removed.
- **Documentation** — The former in-editor docs viewer was removed. The menu command now opens the external GitHub wiki instead.

### Fixed
- **Meta files** — Added missing `.meta` files to avoid Unity warnings.
- **Dictionary loading** — Fixed issue where dictionaries were not recognized right after first install.
- **Popup UI** — Corrected layout so it no longer overflows when using the English interface.


---

## [1.0.0] - 2025-09-23
### Added
- **Exit popup** to write a quick note before quitting Unity (with option to cancel the quit).
- **Startup reminder** that shows your last session/day note when the editor opens.
- **Notes Browser** with free-text search and clickable references (Ping/Open assets & scenes).
- **Preferences panel** (Edit → Preferences → *Where I Left Off*) to enable/disable popups, choose startup mode, and clear temporary drafts.
- **Localization auto-detect**: Spanish UI & docs if Unity/OS is Spanish, otherwise English.
- **Open Documentation** menu entry.
- **In-Editor Docs Viewer** window that renders the README with headings, bold/italics, lists, quotes and code blocks.
- **Localized README files**: `README.es.md` and `README.en.md` in package root.
- **CHANGELOG.md** and **LICENSE** included in package root.
- **String tables** via `Editor/Resources` for future UI text localization.

### Changed
- **Package metadata** (`package.json`): English description, keywords, min Unity version (`6000.0`), optional `unityRelease`, documentation & changelog URLs.
- **Menu priorities** unified to avoid unintended separators between items.
- **Open Documentation** now picks the preferred README based on locale with graceful fallbacks.
- **Project structure** standardized for UPM: docs & metadata in package root; editor code under `Editor/` only.

### Fixed
- Robust package root resolution using `UnityEditor.PackageManager.PackageInfo.FindForAssembly(...)` (avoids `IndexOutOfRangeException` from `FindAssets()[0]`).
- Fallback logic for README selection (tries ES first on Spanish systems, otherwise EN; falls back to the other if missing).
