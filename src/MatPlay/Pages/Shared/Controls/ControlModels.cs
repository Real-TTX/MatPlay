namespace MatPlay.Pages.Shared.Controls;

/// <summary>Modelle der wiederverwendbaren UI-Controls (Toolbar, Pagination, Tabs) auf Basis des MatUi-Toolkits.</summary>
public class ListToolbarModel
{
    /// <summary>Eindeutige Id, damit mehrere Toolbars (und ihre Dialoge) auf einer Seite koexistieren.</summary>
    public string Id { get; set; } = "toolbar";
    public string Action { get; set; } = "";
    public string? Search { get; set; }
    public string SearchPlaceholder { get; set; } = "Suchen …";
    public List<ToolbarFilter> Filters { get; set; } = [];
    /// <summary>Die erste Option ist die Standardsortierung (Button leuchtet nur bei abweichender Wahl).</summary>
    public List<(string Value, string Text)> SortOptions { get; set; } = [];
    public string? Sort { get; set; }
    /// <summary>Zusätzliche Hidden-Felder (z.B. um Filter anderer Controls zu erhalten).</summary>
    public Dictionary<string, string> Hidden { get; set; } = [];
}

public class ToolbarFilter
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public List<(string Value, string Text)> Options { get; set; } = [];
    public string? Selected { get; set; }
}

public class PaginationModel
{
    public string Id { get; set; } = "pagination";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public required Func<int, string> UrlFor { get; set; }
}

/// <summary>Button-Zeile unter Formularen: Speichern, Zurück, Abstand, Löschen (FORM-01).</summary>
public class FormActionsModel
{
    public string SaveText { get; set; } = "Speichern";
    public string SaveIcon { get; set; } = "save";
    public string? BackUrl { get; set; }
    /// <summary>Name des Page-Handlers (z.B. "Delete"); leer = kein Löschen-Button.</summary>
    public string? DeleteHandler { get; set; }
    public string DeleteConfirm { get; set; } = "Wirklich löschen?";
    public Dictionary<string, string> DeleteRouteValues { get; set; } = [];
}

public class TabsModel
{
    public string Id { get; set; } = "tabs";
    public List<(string Key, string Label, string Url)> Items { get; set; } = [];
    public string? Active { get; set; }
}
