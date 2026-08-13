namespace DevelopmentHub.Workflow.Steps;

/// <summary>Transport used to fetch an Azure DevOps artifact.</summary>
public enum ArtifactDownloadMethod
{
    /// <summary>Prefer the Azure CLI when it is installed and the step writes extracted content, otherwise use REST.</summary>
    Auto,

    /// <summary>Force <c>az pipelines runs artifact download</c> (chunked, resumable, no intermediate ZIP).</summary>
    AzureCli,

    /// <summary>Force the Azure DevOps REST API (single ZIP stream).</summary>
    Rest,
}
