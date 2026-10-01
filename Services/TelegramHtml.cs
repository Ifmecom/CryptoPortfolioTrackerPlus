using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure hulpfuncties voor Telegram-HTML (parse_mode=HTML). Telegram weigert het hele bericht bij één
/// los '&lt;' (bijv. "RSI &lt;30") of een onbekende tag. <see cref="Sanitize"/> laat de door Telegram
/// ondersteunde tags staan en codeert al het andere.
/// </summary>
public static class TelegramHtml
{
    // Door Telegram ondersteunde tags (https://core.telegram.org/bots/api#html-style).
    private static readonly Regex AllowedTag = new(
        @"\G</?(b|strong|i|em|u|ins|s|strike|del|code|pre|blockquote|tg-spoiler)>" +
        @"|\G<a\s+href=""[^""<>]*"">|\G</a>" +
        @"|\G<span\s+class=""tg-spoiler"">|\G</span>" +
        @"|\G<code\s+class=""language-[\w-]+"">" +
        @"|\G<blockquote\s+expandable>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Entity = new(
        @"\G&(lt|gt|amp|quot|#\d+|#x[0-9a-fA-F]+);", RegexOptions.Compiled);

    private static readonly Regex AnyTag = new(@"<[^<>]*>", RegexOptions.Compiled);

    /// <summary>Codeert tekst zodat hij letterlijk in een Telegram-HTML-bericht kan.</summary>
    public static string Escape(string? text)
        => string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// Maakt een (deels) HTML-bericht veilig: ondersteunde tags en geldige entities blijven, elk ander
    /// '&lt;', '&gt;' of '&amp;' wordt gecodeerd.
    /// </summary>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        var sb = new StringBuilder(html.Length + 16);
        int i = 0;
        while (i < html.Length)
        {
            char c = html[i];
            if (c == '<')
            {
                var m = AllowedTag.Match(html, i);
                if (m.Success) { sb.Append(m.Value); i += m.Length; continue; }
                sb.Append("&lt;");
            }
            else if (c == '&')
            {
                var m = Entity.Match(html, i);
                if (m.Success) { sb.Append(m.Value); i += m.Length; continue; }
                sb.Append("&amp;");
            }
            else if (c == '>') sb.Append("&gt;");
            else sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>Platte-tekstversie (tags weg, entities gedecodeerd) — vangnet als HTML toch geweigerd wordt.</summary>
    public static string ToPlainText(string? html)
        // Eerst saneren: dan zijn alleen nog echte tags '<…>' en blijft "RSI <30 en >70" tekst.
        => string.IsNullOrEmpty(html) ? string.Empty : WebUtility.HtmlDecode(AnyTag.Replace(Sanitize(html), string.Empty));
}
