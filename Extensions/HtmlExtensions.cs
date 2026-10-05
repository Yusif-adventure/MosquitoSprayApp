using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SmartMosquitoControl.Extensions;

public static class HtmlExtensions
{
    /// <summary>
    /// Renders a UTC timestamp as a &lt;time&gt; element. The browser rewrites it into the viewer's own
    /// time zone (see wwwroot/js/local-time.js); the text inside is a UTC fallback for no-JS clients.
    /// Formats: datetime, date, day, time.
    /// </summary>
    public static IHtmlContent LocalTime(this IHtmlHelper html, DateTime utc, string format = "datetime")
    {
        var value = utc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            : utc.ToUniversalTime();

        var tag = new TagBuilder("time");
        tag.Attributes["datetime"] = value.ToString("o", CultureInfo.InvariantCulture);
        tag.Attributes["data-local-time"] = format;
        tag.InnerHtml.Append(value.ToString("ddd, MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture) + " UTC");
        return tag;
    }
}
