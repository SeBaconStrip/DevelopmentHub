namespace DevelopmentHub.Api.Models.Dtos;

public class DashboardConfigDto
{
    public List<DashboardWidgetDto> Widgets { get; set; } = [];
    public Dictionary<string, List<LayoutItemDto>> GridLayouts { get; set; } = new();
}

public class DashboardWidgetDto
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public class LayoutItemDto
{
    public string I { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int? MinW { get; set; }
    public int? MinH { get; set; }
}
