namespace LedgerFlow.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string UploadsPath { get; set; } = "App_Data/uploads";

    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
}