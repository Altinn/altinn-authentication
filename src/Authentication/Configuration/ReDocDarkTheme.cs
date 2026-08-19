using System.Collections.Generic;
using Swashbuckle.AspNetCore.ReDoc;

namespace Altinn.Platform.Authentication.Configuration
{
    /// <summary>
    /// Applies a dark colour scheme to a ReDoc page.
    /// </summary>
    /// <remarks>
    /// ReDoc has no built-in dark mode. Most of the page is themeable through its <c>theme</c>
    /// config object, which is the reliable route - it feeds ReDoc's own styling rather than
    /// fighting the hashed class names its CSS-in-JS generates. The content panel background is
    /// not part of that object, so a small stylesheet covers what the theme cannot reach.
    /// </remarks>
    public static class ReDocDarkTheme
    {
        private const string Canvas = "#0d1117";
        private const string Panel = "#161b22";
        private const string Border = "#30363d";
        private const string TextPrimary = "#c9d1d9";
        private const string TextSecondary = "#8b949e";
        private const string Accent = "#58a6ff";

        /// <summary>
        /// Applies the dark scheme to the given ReDoc page.
        /// </summary>
        /// <param name="options">The ReDoc options to configure.</param>
        public static void Apply(ReDocOptions options)
        {
            options.ConfigObject.AdditionalItems["theme"] = BuildTheme();
            options.HeadContent = Stylesheet;
        }

        private static Dictionary<string, object> BuildTheme() => new()
        {
            ["colors"] = new Dictionary<string, object>
            {
                ["primary"] = new Dictionary<string, object> { ["main"] = Accent },
                ["text"] = new Dictionary<string, object>
                {
                    ["primary"] = TextPrimary,
                    ["secondary"] = TextSecondary,
                },
                ["border"] = new Dictionary<string, object>
                {
                    ["dark"] = Border,
                    ["light"] = Border,
                },
                ["http"] = new Dictionary<string, object>
                {
                    ["get"] = "#3fb950",
                    ["post"] = Accent,
                    ["put"] = "#d29922",
                    ["patch"] = "#d29922",
                    ["delete"] = "#f85149",
                },
            },
            ["schema"] = new Dictionary<string, object>
            {
                ["nestedBackground"] = Panel,
                ["typeNameColor"] = TextSecondary,
                ["typeTitleColor"] = TextPrimary,
            },
            ["sidebar"] = new Dictionary<string, object>
            {
                ["backgroundColor"] = Panel,
                ["textColor"] = TextPrimary,
                ["activeTextColor"] = Accent,
                ["groupItems"] = new Dictionary<string, object> { ["textTransform"] = "none" },
            },
            ["rightPanel"] = new Dictionary<string, object>
            {
                ["backgroundColor"] = Panel,
                ["textColor"] = TextPrimary,
            },
            ["codeBlock"] = new Dictionary<string, object>
            {
                ["backgroundColor"] = Canvas,
            },
        };

        /// <summary>
        /// Covers the surfaces ReDoc's theme object does not expose - chiefly the page and the
        /// middle content panel, which are otherwise left white.
        /// </summary>
        private static string Stylesheet =>
            $$"""
            <style>
              html, body { background-color: {{Canvas}}; color: {{TextPrimary}}; }
              .redoc-wrap, .api-content, [data-section-id] { background-color: {{Canvas}}; }
              h1, h2, h3, h4, h5 { color: #e6edf3 !important; }
              a { color: {{Accent}}; }
              table td, table th { border-color: {{Border}} !important; }
              /* Search box and the operation panels sit on the canvas, not on white. */
              .menu-content, .search-input, .operation-type { background-color: {{Panel}} !important; }
            </style>
            """;
    }
}
