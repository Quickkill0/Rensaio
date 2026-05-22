using System.Text.Json.Serialization;

namespace KaizokuBackend.Models.Dto;

public class ChapterDto
{
    [JsonPropertyName("number")]
    public decimal? Number { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("pageCount")]
    public int? PageCount { get; set; }

    [JsonPropertyName("providerUploadDate")]
    public DateTime? ProviderUploadDate { get; set; }

    [JsonPropertyName("downloadDate")]
    public DateTime? DownloadDate { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("status")]
    public ChapterDownloadStatus Status { get; set; }

    [JsonPropertyName("providers")]
    public List<ChapterProviderDto> Providers { get; set; } = new();
}

public class ChapterProviderDto
{
    [JsonPropertyName("providerId")]
    public Guid ProviderId { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("scanlator")]
    public string? Scanlator { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("providerIndex")]
    public int ProviderIndex { get; set; }

    [JsonPropertyName("isDownloaded")]
    public bool IsDownloaded { get; set; }
}

public enum ChapterDownloadStatus
{
    Missing = 0,
    Queued = 1,
    Downloaded = 2,
    Failed = 3
}

public class DownloadChaptersRequestDto
{
    [JsonPropertyName("chapterNumbers")]
    public decimal[]? ChapterNumbers { get; set; }
}

public class DownloadMissingResultDto
{
    [JsonPropertyName("enqueuedCount")]
    public int EnqueuedCount { get; set; }
}

public class RefreshChaptersResultDto
{
    [JsonPropertyName("jobsEnqueued")]
    public int JobsEnqueued { get; set; }
}
