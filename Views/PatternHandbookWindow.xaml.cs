using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WinRT.Interop;

namespace CryptoPortfolioTracker.Views;

/// <summary>
/// Non-modal window that renders PATTERN_HANDBOOK.md as a styled HTML page
/// inside a WebView2.  No external CDN is used — markdown is converted to HTML
/// entirely in C#, so the window works fully offline.
/// </summary>
public sealed partial class PatternHandbookWindow : Window
{
    public PatternHandbookWindow()
    {
        InitializeComponent();
        Title = "📖 Pattern Handboek";
        SetWindowSize(980, 820);
    }

    private void SetWindowSize(int width, int height)
    {
        var hwnd    = WindowNative.GetWindowHandle(this);
        var wndId   = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWnd  = AppWindow.GetFromWindowId(wndId);
        var display = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);

        int x = Math.Max(0, (display.WorkArea.Width  - width)  / 2);
        int y = Math.Max(0, (display.WorkArea.Height - height) / 2);

        appWnd.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
    }

    // ── WebView2 lifecycle ───────────────────────────────────────────────────

    private async void HandbookView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await HandbookView.EnsureCoreWebView2Async();

            string md   = ReadHandbook();
            string html = BuildHtml(md);
            HandbookView.NavigateToString(html);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PatternHandbookWindow: WebView2 init failed — {ex.Message}");
        }
    }

    // ── File loading ─────────────────────────────────────────────────────────

    private static string ReadHandbook()
    {
        // Try next to the executable first (deployed / built output)
        string path = Path.Combine(AppContext.BaseDirectory, "PATTERN_HANDBOOK.md");
        if (File.Exists(path))
            return File.ReadAllText(path, Encoding.UTF8);

        // Fall back to project root (running from Visual Studio debug profile)
        try
        {
            string? projectRoot = FindProjectRoot(AppContext.BaseDirectory);
            if (projectRoot is not null)
            {
                string fallback = Path.Combine(projectRoot, "PATTERN_HANDBOOK.md");
                if (File.Exists(fallback))
                    return File.ReadAllText(fallback, Encoding.UTF8);
            }
        }
        catch { /* ignore */ }

        return "# Handboek niet gevonden\n\n"
             + "`PATTERN_HANDBOOK.md` staat niet in de applicatiemap.\n\n"
             + "Controleer of het bestand is opgenomen in het project met "
             + "**CopyToOutputDirectory = PreserveNewest**.";
    }

    /// <summary>Walk up the directory tree looking for a .csproj file.</summary>
    private static string? FindProjectRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (dir.GetFiles("*.csproj").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    // ── HTML generation ──────────────────────────────────────────────────────

    private static string BuildHtml(string markdown)
    {
        string body = ConvertMarkdownToHtml(markdown);

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
*{{box-sizing:border-box;margin:0;padding:0}}
html{{scroll-behavior:smooth}}
body{{
  background:#131722;
  color:#c8c8d0;
  font-family:-apple-system,'Segoe UI',Roboto,sans-serif;
  font-size:14px;
  line-height:1.7;
  padding:28px 40px 64px;
  max-width:920px;
}}
h1{{color:#b8860b;font-size:22px;margin:28px 0 10px;border-bottom:1px solid #2a2e39;padding-bottom:8px}}
h2{{color:#daa520;font-size:18px;margin:24px 0 6px;padding-top:4px}}
h3{{color:#f0c040;font-size:15px;margin:18px 0 4px}}
h4{{color:#e8e8e8;font-size:14px;margin:14px 0 4px;font-style:italic}}
p{{margin:5px 0}}
strong{{color:#ffffff}}
em{{color:#a0b8d0;font-style:italic}}
code{{
  background:#1e2230;
  color:#7ec8e3;
  padding:1px 5px;
  border-radius:3px;
  font-family:'Cascadia Code','Consolas',monospace;
  font-size:12.5px;
  white-space:nowrap;
}}
hr{{border:none;border-top:1px solid #2a2e39;margin:18px 0}}
ul{{margin:5px 0 5px 22px}}
ul.sub{{margin:3px 0 3px 22px}}
li{{margin:3px 0}}
blockquote{{
  border-left:3px solid #4a90e2;
  background:#1a2030;
  padding:8px 14px;
  margin:8px 0;
  border-radius:0 4px 4px 0;
  color:#a0c4d8;
}}
.table-wrap{{overflow-x:auto;margin:10px 0}}
table{{border-collapse:collapse;width:100%;min-width:360px}}
th{{
  background:#2a2e39;
  color:#daa520;
  text-align:left;
  padding:7px 12px;
  border:1px solid #3a3e4a;
  font-weight:600;
  white-space:nowrap;
}}
td{{
  padding:6px 12px;
  border:1px solid #2a2e39;
  vertical-align:top;
}}
tr:nth-child(even) td{{background:#181f2e}}
.spacer{{height:6px}}
</style>
</head>
<body>
{body}
</body>
</html>";
    }

    // ── Markdown → HTML converter ────────────────────────────────────────────
    // Handles: h1–h4, hr, tables with optional header row, unordered/ordered
    // lists + one sub-level, blockquotes, **bold**, `inline code`, paragraphs.
    // No external dependencies — works fully offline.

    private static string ConvertMarkdownToHtml(string markdown)
    {
        var lines = markdown.Split('\n');
        var sb    = new StringBuilder(markdown.Length * 2);
        bool inUl    = false;
        bool inSubUl = false;
        int  i       = 0;

        void CloseList()
        {
            if (inSubUl) { sb.AppendLine("</ul>"); inSubUl = false; }
            if (inUl)    { sb.AppendLine("</ul>"); inUl    = false; }
        }

        while (i < lines.Length)
        {
            string raw     = lines[i].TrimEnd('\r');
            string trimmed = raw.Trim();

            // ── Table block: collect all consecutive | lines ──────────────
            if (trimmed.StartsWith("|"))
            {
                CloseList();
                var tableRows = new List<string>();
                while (i < lines.Length && lines[i].TrimEnd('\r').Trim().StartsWith("|"))
                {
                    tableRows.Add(lines[i].TrimEnd('\r'));
                    i++;
                }
                sb.AppendLine(RenderTable(tableRows));
                continue;
            }

            // ── List items ────────────────────────────────────────────────
            bool isSubItem  = raw.StartsWith("  - ") || raw.StartsWith("   - ")
                           || raw.StartsWith("    - ") || raw.StartsWith("\t- ");
            bool isListItem = !isSubItem
                           && (raw.TrimStart().StartsWith("- ")
                              || Regex.IsMatch(raw.TrimStart(), @"^\d+\. "));

            if (isListItem)
            {
                if (inSubUl) { sb.AppendLine("</ul>"); inSubUl = false; }
                if (!inUl)   { sb.AppendLine("<ul>");  inUl    = true;  }
                string content = Regex.IsMatch(raw.TrimStart(), @"^\d+\. ")
                    ? Regex.Replace(raw.TrimStart(), @"^\d+\. ", "")
                    : raw.TrimStart().Substring(2);
                sb.AppendLine($"  <li>{ApplyInline(content)}</li>");
                i++;
                continue;
            }

            if (isSubItem)
            {
                if (!inUl)    { sb.AppendLine("<ul>");               inUl    = true; }
                if (!inSubUl) { sb.AppendLine("<ul class=\"sub\">"); inSubUl = true; }
                string content = raw.TrimStart().Substring(2);
                sb.AppendLine($"    <li>{ApplyInline(content)}</li>");
                i++;
                continue;
            }

            // Not a list item — close open lists
            CloseList();

            // ── Other block elements ──────────────────────────────────────
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                sb.AppendLine("<div class=\"spacer\"></div>");
            }
            else if (trimmed.StartsWith("#### "))
            {
                sb.AppendLine($"<h4>{ApplyInline(trimmed.Substring(5))}</h4>");
            }
            else if (trimmed.StartsWith("### "))
            {
                sb.AppendLine($"<h3>{ApplyInline(trimmed.Substring(4))}</h3>");
            }
            else if (trimmed.StartsWith("## "))
            {
                sb.AppendLine($"<h2>{ApplyInline(trimmed.Substring(3))}</h2>");
            }
            else if (trimmed.StartsWith("# "))
            {
                sb.AppendLine($"<h1>{ApplyInline(trimmed.Substring(2))}</h1>");
            }
            else if (Regex.IsMatch(trimmed, @"^-{3,}$"))
            {
                sb.AppendLine("<hr/>");
            }
            else if (trimmed.StartsWith("> "))
            {
                sb.AppendLine($"<blockquote>{ApplyInline(trimmed.Substring(2))}</blockquote>");
            }
            else
            {
                sb.AppendLine($"<p>{ApplyInline(trimmed)}</p>");
            }

            i++;
        }

        CloseList();
        return sb.ToString();
    }

    private static string RenderTable(List<string> rows)
    {
        var sb = new StringBuilder("<div class=\"table-wrap\"><table>");

        // If row[1] is a separator line (|---|---|), row[0] is the header
        bool hasHeader = rows.Count >= 2
                      && Regex.IsMatch(rows[1].Trim(), @"^\|[\s\-\|:]+\|$");
        bool tbodyOpen = false;

        for (int r = 0; r < rows.Count; r++)
        {
            string row = rows[r].Trim();

            // Skip separator/alignment rows
            if (Regex.IsMatch(row, @"^\|[\s\-\|:]+\|$")) continue;

            string[] cells = row.Split('|', StringSplitOptions.RemoveEmptyEntries);

            if (hasHeader && r == 0)
            {
                sb.Append("<thead><tr>");
                foreach (string cell in cells)
                    sb.Append($"<th>{ApplyInline(cell.Trim())}</th>");
                sb.Append("</tr></thead>");
            }
            else
            {
                if (!tbodyOpen) { sb.Append("<tbody>"); tbodyOpen = true; }
                sb.Append("<tr>");
                foreach (string cell in cells)
                    sb.Append($"<td>{ApplyInline(cell.Trim())}</td>");
                sb.Append("</tr>");
            }
        }

        if (tbodyOpen) sb.Append("</tbody>");
        sb.Append("</table></div>");
        return sb.ToString();
    }

    /// <summary>
    /// HTML-escapes the text first, then applies **bold** and `code` inline markdown.
    /// </summary>
    private static string ApplyInline(string text)
    {
        // HTML-escape special chars before adding any markup
        text = text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        // **bold**
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        // *italic*
        text = Regex.Replace(text, @"\*([^*]+?)\*", "<em>$1</em>");
        // `inline code`
        text = Regex.Replace(text, @"`([^`]+?)`", "<code>$1</code>");

        return text;
    }
}
