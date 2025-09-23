// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using System;
using System.Collections.Generic;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// WILO note entry. Represents a note with date/time metadata,
    /// session identifier, title, message, and asset reference GUIDs.
    /// </summary>
    [Serializable]
    public class WiloNote
    {
        /// <summary>
        /// Human “working day” in <c>yyyy-MM-dd</c> format (e.g., "2025-09-23").
        /// Used to group notes by day.
        /// </summary>
        public string day;          // "yyyy-MM-dd"

        /// <summary>
        /// UTC timestamp in full ISO-8601 round-trip format.
        /// Tip: <c>DateTime.UtcNow.ToString("o")</c>.
        /// </summary>
        public string utc;          // DateTime.UtcNow.ToString("o")

        /// <summary>
        /// Editor session identifier (to be filled when SessionID is integrated).
        /// Helps distinguish notes created in different sessions.
        /// </summary>
        public string sessionId;    // to be filled once SessionID is integrated

        /// <summary>Short note title.</summary>
        public string title;

        /// <summary>Main note content (free text).</summary>
        public string message;

        /// <summary>
        /// Related asset GUIDs (scenes, prefabs, materials, etc.).
        /// Stored as text for simple (JSON) persistence.
        /// </summary>
        public List<string> refs = new List<string>();
    }
    
    /// <summary>
    /// Root container used to serialize a WILO notes file.
    /// Handy to save/load a collection of <see cref="WiloNote"/>.
    /// </summary>
    [Serializable]
    public class WiloNotesFile
    {
        /// <summary>Stored notes.</summary>
        public List<WiloNote> notes = new List<WiloNote>();
    }
}