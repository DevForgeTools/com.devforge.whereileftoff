# Where I Left Off — Fast exit notes for Unity

> Leave a note when quitting. See it on next launch.

**Where I Left Off** lets you write a quick note **before quitting Unity** and shows it **on startup**. It also includes a searchable **Browser** to review notes and open project references.

## ✨ Features
- **Exit popup**: jot a note before quitting (or cancel quit).
- **Startup reminder**: shows last session/day note on launch.
- **Searchable Browser**: filter by text/date; ping/open references.
- **Zero-friction**: no external services, no mandatory setup.
- **Local data**: JSON under `Library/` (keeps VCS clean).
- **Auto-locale**: Spanish UI & docs if your Unity/OS is Spanish; otherwise English.

## 📦 Install (UPM)
1. Open **Window → Package Manager**.
2. Click **Add package from disk…** and pick `package.json` in `Packages/com.devforge.whereileftoff/`.
   - Alternatively, add the repo/URL to your project’s `manifest.json`.

## 🚀 Quickstart (60 s)
1. Work as usual.
2. On **quit**, write a **one-liner** in the popup and save.
3. On **start**, you’ll see the reminder. To manage notes:
   - **Tools → DevForge → Where I Left Off → Browser**

## 🧭 Menús
- **Tools → DevForge → Where I Left Off → Browser**
- **Tools → DevForge → Where I Left Off → Open Documentation**

## ⚙️ Settings
Open **Edit → Preferences… → Where I Left Off** (or the ⚙️ in the Browser).

Options:
- Show popup on **Quit**.
- Show popup on **Startup** (last session or last day).
- Clear temporary drafts.
- Force language (optional, testing): `wilo.forceLang = ES | EN`.

## 📁 Data
- Notes: `Library/WhereILeftOff/*.json`
- User state: `Library/WhereILeftOff/User/*`

> These files **shouldn’t** be versioned. To migrate between machines, copy them manually.

## 🖱️ Usage
- **Quit**: write and save. You can **cancel** quitting.
- **Open**: you’ll see the reminder; dismiss it or open the **Browser**.
- **Browser**: free-text search, `Ping`/`Open` references, edit or duplicate notes.

## 🧪 Compatibility
- Tested on **Unity 6**.
- **Editor-only** package (excluded from builds).
- IMGUI UI (stable). Optional UI Toolkit support in the future.

## ❓ FAQ
- **Can I disable popups?** Yes, via **Preferences** or from the Browser.
- **Cloud sync?** No. Simple, local by design.
- **Export/Import?** Copy the JSON files under `Library/WhereILeftOff/`.
- **Localization?** Detects Spanish; defaults to English otherwise.

## 💬 Support
- Issues & feedback: open an issue on the repo or contact us.
