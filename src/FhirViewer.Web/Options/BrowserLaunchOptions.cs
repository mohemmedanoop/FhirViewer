namespace FhirViewer.Web.Options;

public sealed class BrowserLaunchOptions
{
    public const string SectionName = "BrowserLaunch";

    public bool Enabled { get; set; } = true;

    public string RelativeUrl { get; set; } = "/";
}
