// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Locale helper for Editor usage.
    /// Honors the forced language set in <see cref="WiloPreferences"/> (Editor only).
    /// </summary>
    internal static class Locale
    {
        /// <summary>Supported UI languages.</summary>
        public enum Lang { EN, ES }

        static Lang? _cached;

        /// <summary>Resolves the current language. Uses a cached value once computed.</summary>
        public static Lang Current
        {
            get
            {
                if (_cached.HasValue) return _cached.Value;

#if UNITY_EDITOR
                switch (WiloPreferences.GetForcedLanguage())
                {
                    case WiloPreferences.ForcedLanguage.English: _cached = Lang.EN; break;
                    case WiloPreferences.ForcedLanguage.Spanish: _cached = Lang.ES; break;
                    default: _cached = Lang.EN; break; // safety
                }
#else
                _cached = Lang.EN; // Editor-only package; if ever reached at runtime, default to EN
#endif
                return _cached.Value;
            }
        }

        /// <summary>Convenience flag for Spanish UI.</summary>
        public static bool IsSpanish => Current == Lang.ES;

        /// <summary>Clears the cached language so the next access recomputes it.</summary>
        public static void ClearCache() => _cached = null;
    }
}