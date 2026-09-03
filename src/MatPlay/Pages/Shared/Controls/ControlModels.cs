namespace MatPlay.Pages.Shared.Controls;

/// <summary>Modelle der wiederverwendbaren UI-Controls (Toolbar, Pagination, Tabs).</summary>
public class ListToolbarModel
{
    /// <summary>Eindeutige Id, damit mehrere Toolbars auf einer Seite koexistieren.</summary>
    public string Id { get; set; } = "toolbar";
    public string Action { get; set; } = "";
    public string? Search { get; set; }
    public string SearchPlaceholder { get; set; } = "Suchen …";
    public List<ToolbarFilter> Filters { get; set; } = [];
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
    public required Func<int, string> UrlFor { get; set; }
}

public class TabsModel
{
    public string Id { get; set; } = "tabs";
    public List<(string Key, string Label, string Url)> Items { get; set; } = [];
    public string? Active { get; set; }
}
