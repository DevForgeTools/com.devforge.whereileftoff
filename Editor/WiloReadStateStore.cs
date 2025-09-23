// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Per-USER+PROJECT read-state store.
    /// Persists JSON to:
    ///   <ProjectRoot>/UserSettings/WILO/read_state.<userTag>.json   (when UserSettings exists)
    ///   <ProjectRoot>/Library/WILO/read_state.<userTag>.json        (fallback)
    ///
    /// userTag = uid-<unityUserId> | <UnityName> | <WiloPreferences.UserAlias> | <OSUser> | anonymous
    /// Includes:
    ///  - In-memory cache (HashSet)
    ///  - Debounced save
    ///  - Adopt/rename/merge when userTag changes (alias/user switch)
    ///  - Optional migration from EditorPrefs (READ_/REREAD_)
    /// </summary>
    internal static class WiloReadStateStore
    {
        // -------------------------- Data types --------------------------

        [Serializable]
        private class Data
        {
            public int schema = 1;
            public string unityUserId;
            public string unityUserName;
            public string osUser;
            public List<string> READ   = new List<string>();
            public List<string> REREAD = new List<string>();
        }

        /// <summary>Minimal structure used to merge JSON files when adopting a new tag.</summary>
        [Serializable]
        private class MinimalState
        {
            public int schema;
            public List<string> READ;
            public List<string> REREAD;
        }

        // -------------------------- Internal state --------------------------

        private static readonly HashSet<string> _read   = new HashSet<string>();
        private static readonly HashSet<string> _reread = new HashSet<string>();

        private static string _filePath;
        private static bool _loaded;
        private static bool _dirty;
        private static double _saveAt = -1;
        
        static string _currentTag;

        // -------------------------- Public API (used by WiloUtilities) --------------------------

        public static bool IsRead(string utc)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(utc) && _read.Contains(utc);
        }

        public static void SetRead(string utc, bool v)
        {
            if (string.IsNullOrEmpty(utc)) return;
            EnsureLoaded();
            if (v ? _read.Add(utc) : _read.Remove(utc))
                TouchDirty();
        }

        public static void SetReadBulk(IEnumerable<string> utcs, bool v)
        {
            EnsureLoaded();
            bool changed = false;
            if (utcs != null)
            {
                foreach (var u in utcs)
                {
                    if (string.IsNullOrEmpty(u)) continue;
                    changed |= v ? _read.Add(u) : _read.Remove(u);
                }
            }
            if (changed) TouchDirty();
        }

        public static bool IsReRead(string utc)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(utc) && _reread.Contains(utc);
        }

        public static void SetReRead(string utc, bool v)
        {
            if (string.IsNullOrEmpty(utc)) return;
            EnsureLoaded();
            if (v ? _reread.Add(utc) : _reread.Remove(utc))
                TouchDirty();
        }

        /// <summary>
        /// Optional migration from EditorPrefs (legacy system). Call once, passing ALL notes.
        /// Skips when JSON store already contains data.
        /// </summary>
        public static void MigrateFromEditorPrefsIfNeeded(IEnumerable<WiloNote> allNotes)
        {
            EnsureLoaded();
            if (_read.Count > 0 || _reread.Count > 0) return; // already have JSON data → do not migrate

            bool changed = false;
            if (allNotes != null)
            {
                foreach (var n in allNotes)
                {
                    if (n == null || string.IsNullOrEmpty(n.utc)) continue;

                    var readKey   = ProjectKey("READ_" + n.utc);
                    var rereadKey = ProjectKey("REREAD_" + n.utc);

                    try
                    {
                        if (EditorPrefs.HasKey(readKey) && EditorPrefs.GetBool(readKey))
                            changed |= _read.Add(n.utc);
                        if (EditorPrefs.HasKey(rereadKey) && EditorPrefs.GetBool(rereadKey))
                            changed |= _reread.Add(n.utc);
                    }
                    catch { /* ignore */ }
                }
            }

            if (changed) TouchDirty();
        }

        // -------------------------- Load / save --------------------------

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            // Compute current tag/path
            _currentTag = GetUserTag();
            _filePath   = ResolveFilePath();

            // If there is any file with a different tag under the base folder, adopt it (move/merge the most recent one)
            if (!string.IsNullOrEmpty(projectRoot))
                MaybeAdoptPreviousState(projectRoot, _currentTag);

            // Ensure directory exists
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch { /* ignore */ }

            // Load JSON if present
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var data = JsonUtility.FromJson<Data>(json);
                    _read.Clear(); _reread.Clear();
                    if (data?.READ   != null) foreach (var s in data.READ)   if (!string.IsNullOrEmpty(s)) _read.Add(s);
                    if (data?.REREAD != null) foreach (var s in data.REREAD) if (!string.IsNullOrEmpty(s)) _reread.Add(s);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WILO] Could not read read_state.json: {e.Message}");
                }
            }

            _loaded = true;

            // Debounced saver tick
            EditorApplication.update -= UpdateSaver;
            EditorApplication.update += UpdateSaver;
        }

        /// <summary>Periodic saver: writes to disk when the debounce delay expires.</summary>
        private static void UpdateSaver()
        {
            if (!_dirty) return;
            if (_saveAt < 0) return;
            if (EditorApplication.timeSinceStartup >= _saveAt)
            {
                _saveAt = -1;
                _dirty = false;
                SaveNow();
            }
        }

        /// <summary>Marks the store dirty and schedules a save after a short debounce.</summary>
        private static void TouchDirty()
        {
            _dirty = true;
            _saveAt = EditorApplication.timeSinceStartup + 0.30; // ~300ms debounce
        }

        /// <summary>Immediate save to disk (after ensuring the path/tag is up to date).</summary>
        private static void SaveNow()
        {
            // If the tag changed at runtime (e.g., user set an alias), migrate to the new filename
            EnsureFreshPath();

            try
            {
                var (uid, uname) = TryGetUnityUser();
                var data = new Data
                {
                    schema = 1,
                    unityUserId = uid,
                    unityUserName = uname,
                    osUser = Environment.UserName,
                    READ   = new List<string>(_read),
                    REREAD = new List<string>(_reread)
                };

                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WILO] Error saving read_state.json: {e.Message}");
            }
        }

        // -------------------------- Paths / user tag --------------------------

        /// <summary>Resolves the full path for the current read-state file based on UserSettings/Library.</summary>
        private static string ResolveFilePath()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                // Last-resort fallback: system temp folder
                var tmpRoot = Path.GetTempPath();
                var tmpDir  = Path.Combine(tmpRoot, "WILO");
                if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
                return Path.Combine(tmpDir, "read_state.anonymous.json");
            }

            var userSettingsDir = Path.Combine(projectRoot, "UserSettings", "WILO");
            var libraryDir      = Path.Combine(projectRoot, "Library", "WILO");

            string targetDir = Directory.Exists(Path.Combine(projectRoot, "UserSettings"))
                ? userSettingsDir
                : libraryDir;

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            string userTag = GetUserTag();
            return Path.Combine(targetDir, $"read_state.{userTag}.json");
        }

        /// <summary>
        /// Determines the best user tag in priority order:
        /// 1) Unity account (id/name) if available
        /// 2) WILO alias (Preferences)
        /// 3) OS user name
        /// 4) "anonymous"
        /// </summary>
        private static string GetUserTag()
        {
            // 1) Unity account (if available)
            var (uid, uname) = TryGetUnityUser();
            if (!string.IsNullOrEmpty(uid))   return $"uid-{uid}";
            if (!string.IsNullOrEmpty(uname)) return Sanitize(uname);

            // 2) WILO alias (preferences)
            try
            {
                var alias = WiloPreferences.UserAlias; // public preference
                if (!string.IsNullOrWhiteSpace(alias)) return Sanitize(alias);
            }
            catch { /* prefs may not be available yet */ }

            // 3) OS user
            var os = Environment.UserName;
            if (!string.IsNullOrWhiteSpace(os)) return Sanitize(os);

            return "anonymous";

            static string Sanitize(string s)
            {
                foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
                return s.Trim();
            }
        }

        // -------------------------- Unity user via reflection (robust) --------------------------

        /// <summary>
        /// Tries to retrieve Unity account info via reflection (UnityEditor.Connect.UnityConnect).
        /// Returns (id, name) when possible; otherwise (null, null).
        /// </summary>
        private static (string id, string name) TryGetUnityUser()
        {
            try
            {
                var connectType = Type.GetType("UnityEditor.Connect.UnityConnect, UnityEditor", throwOnError: false);
                if (connectType == null) return (null, null);

                var instanceProp = connectType.GetProperty(
                    "instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null) return (null, null);

                var userInfoProp = connectType.GetProperty(
                    "userInfo",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var userInfo = userInfoProp?.GetValue(instance);
                if (userInfo == null) return (null, null);

                var uiType = userInfo.GetType();

                bool AnyTrue(params string[] flags)
                {
                    foreach (var f in flags)
                    {
                        var p = uiType.GetProperty(f, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (p != null && p.PropertyType == typeof(bool) && (bool)p.GetValue(userInfo)) return true;
                        var fld = uiType.GetField(f, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (fld != null && fld.FieldType == typeof(bool) && (bool)fld.GetValue(userInfo)) return true;
                    }
                    return false;
                }

                string GetStr(string member)
                {
                    var p = uiType.GetProperty(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(userInfo);
                    var f = uiType.GetField(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(userInfo);
                    return null;
                }

                bool looksLogged = AnyTrue("loggedIn", "isLoggedIn", "isSignedIn", "online", "authenticated", "isAuthenticated");

                var userId      = GetStr("userId");
                var displayName = GetStr("displayName");
                var userName    = GetStr("userName");
                var name = !string.IsNullOrEmpty(displayName) ? displayName : userName;

                if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(name) || looksLogged)
                    return (string.IsNullOrEmpty(userId) ? null : userId,
                            string.IsNullOrEmpty(name)   ? null : name);
            }
            catch { /* fall back silently */ }

            return (null, null);
        }

        // -------------------------- Adoption when alias/user changes --------------------------

        // Looks for "read_state.*.json" in the base folder (excluding the current tag).
        // If candidates are found, pick the MOST RECENT:
        //  - if the destination for the current tag does not exist → MOVE the old file to the new name
        //  - if it exists → MERGE (union of READ/REREAD) and optionally delete the old file
        static void MaybeAdoptPreviousState(string projectRoot, string currentTag)
        {
            string baseDir = Directory.Exists(Path.Combine(projectRoot, "UserSettings"))
                ? Path.Combine(projectRoot, "UserSettings", "WILO")
                : Path.Combine(projectRoot, "Library", "WILO");

            if (!Directory.Exists(baseDir)) return;

            string destPath = Path.Combine(baseDir, $"read_state.{currentTag}.json");

            // Find all read_state.*.json except the “current” one
            var candidates = new List<FileInfo>();
            foreach (var f in Directory.GetFiles(baseDir, "read_state.*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(f);
                if (!name.StartsWith("read_state.", StringComparison.Ordinal)) continue;

                var tag = name.Substring("read_state.".Length);
                if (string.Equals(tag, currentTag, StringComparison.Ordinal)) continue; // same tag → ignore

                candidates.Add(new FileInfo(f));
            }
            if (candidates.Count == 0) return;

            // Choose the most recent
            candidates.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            var latest = candidates[0];

            if (!File.Exists(destPath))
            {
                // Simple case: rename/move
                try { File.Move(latest.FullName, destPath); }
                catch { /* ignore */ }
                return;
            }

            // Destination exists → merge
            try
            {
                var oldData = JsonUtility.FromJson<MinimalState>(File.ReadAllText(latest.FullName));
                var newData = JsonUtility.FromJson<MinimalState>(File.ReadAllText(destPath));

                var read   = new HashSet<string>(newData?.READ   ?? new List<string>());
                var reread = new HashSet<string>(newData?.REREAD ?? new List<string>());

                if (oldData?.READ   != null) foreach (var u in oldData.READ)   if (!string.IsNullOrEmpty(u)) read.Add(u);
                if (oldData?.REREAD != null) foreach (var u in oldData.REREAD) if (!string.IsNullOrEmpty(u)) reread.Add(u);

                var merged = new MinimalState { schema = 1, READ = new List<string>(read), REREAD = new List<string>(reread) };
                File.WriteAllText(destPath, JsonUtility.ToJson(merged, true));

                try { File.Delete(latest.FullName); } catch { /* optional */ }
            }
            catch { /* ignore merge errors */ }
        }

        // -------------------------- Namespacing utils --------------------------

        /// <summary>Builds a project-scoped key for EditorPrefs.</summary>
        private static string ProjectKey(string local)
        {
            int pid = Application.dataPath.GetHashCode();
            return $"WILO_{pid}_{local}";
        }
        
        /// <summary>
        /// Ensures the file path reflects the latest user tag; adopts/merges state when tag changes at runtime.
        /// </summary>
        static void EnsureFreshPath()
        {
            var newTag = GetUserTag();
            if (string.Equals(newTag, _currentTag, StringComparison.Ordinal)) return;

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
                MaybeAdoptPreviousState(projectRoot, newTag);

            _currentTag = newTag;
            _filePath   = ResolveFilePath();
        }

    }
}
