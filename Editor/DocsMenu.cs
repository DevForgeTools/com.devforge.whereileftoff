// Copyright (c) 2025 Miguel Ángel García Lucena (Maglucen)
// SPDX-License-Identifier: MIT
// Part of "DevForge · Where I Left Off (WILO)". See LICENSE for details.

using UnityEditor;
using UnityEngine;

namespace DevForge.Wilo.Editor
{
    /// <summary>
    /// Provides utility methods for opening external documentation pages
    /// related to this package.
    /// </summary>
    internal static class Docs
    {
        private const string WikiEs =
            "https://github.com/DevForgeTools/com.devforge.whereileftoff/wiki/Documentaci%C3%B3n-%E2%80%90-Espa%C3%B1ol";

        private const string WikiEn =
            "https://github.com/DevForgeTools/com.devforge.whereileftoff/wiki/Documentation-%E2%80%90-English";

        /// <summary>
        /// Opens the external wiki documentation page in the system browser.
        /// Language is selected depending on <see cref="Locale.IsSpanish"/>.
        /// </summary>
        internal static void OpenDocsExternally()
        {
            var url = Locale.IsSpanish ? WikiEs : WikiEn;
            Application.OpenURL(url);
        }
    }

    /// <summary>
    /// Adds a menu item under Unity's editor to open the package documentation.
    /// </summary>
    internal static class DocsMenu
    {
        // Menu entry for documentation. Kept minimal: always opens external wiki.
        private const string MenuPath = "Tools/DevForge/Where I Left Off/Documentation";

        [MenuItem(MenuPath, priority = 3000)]
        private static void OpenDocs() => Docs.OpenDocsExternally();

        [MenuItem(MenuPath, validate = true)]
        private static bool ValidateOpenDocs() => true;
    }
}