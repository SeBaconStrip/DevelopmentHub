namespace DevelopmentHub.Api.Models.Dtos;

public class WindowsServiceDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool CanStart { get; set; }
    public bool CanStop { get; set; }
}

public class WindowsServiceSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
