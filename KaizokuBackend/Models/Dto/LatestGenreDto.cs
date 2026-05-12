using System.Text.Json.Serialization;

namespace KaizokuBackend.Models.Dto;

/// <summary>
/// A genre/tag available from the cached "Latest" cloud catalogue, along with
/// how many recently fetched titles carry it. Used to populate the tag filter
/// chips on the browse screen.
/// </summary>
// [Schema] // Controller I/O Model
public class LatestGenreDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
