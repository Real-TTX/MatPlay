using Microsoft.AspNetCore.Html;

namespace MatPlay;

/// <summary>Kleine View-Helfer für das MatUi-Toolkit (Icon-Sprite, deutsche Toolkit-Texte).</summary>
public static class Ui
{
    private static string? _sprite;

    /// <summary>Ein Icon aus dem Sprite (matui-icons.svg), wird per &lt;use&gt; referenziert.</summary>
    public static IHtmlContent Icon(string name, string? cssClass = null)
    {
        var cls = string.IsNullOrEmpty(cssClass) ? "mat-icon" : "mat-icon " + cssClass;
        return new HtmlString($"<svg class=\"{cls}\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#mat-i-{name}\"></use></svg>");
    }

    /// <summary>Der Sprite, einmal gelesen und pro Seite einmal inline am Anfang des Body ausgegeben.</summary>
    public static IHtmlContent Sprite(string webRootPath)
    {
        _sprite ??= File.ReadAllText(Path.Combine(webRootPath, "matui", "matui-icons.svg"));
        return new HtmlString(_sprite);
    }

    /// <summary>Texte, die das Toolkit selbst schreibt (Dialoge, Kopieren-Hinweis), auf Deutsch – gehören ans &lt;html&gt;-Element.</summary>
    public static IHtmlContent ToolkitTexts { get; } = new HtmlString(
        "data-dlg-ok=\"OK\" data-dlg-cancel=\"Abbrechen\" data-dlg-close=\"Schließen\" " +
        "data-dlg-confirm=\"Bitte bestätigen\" data-dlg-notice=\"Hinweis\" data-dlg-input=\"Eingabe\" " +
        "data-copied=\"Kopiert\" data-pk-apply=\"Fertig\" data-pk-clear=\"Leeren\"");

    /// <summary>Bis zu zwei Initialen für den Avatar.</summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = parts[0][..1];
        var second = parts.Length > 1 ? parts[^1][..1] : string.Empty;
        return (first + second).ToUpperInvariant();
    }
}
