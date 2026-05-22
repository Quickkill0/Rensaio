import { apiClient } from '@/lib/api/client';
import { type FullSeries, type SeriesInfo, type SeriesExtendedInfo, type ProviderMatch, type AugmentedResponse, type LatestSeriesInfo, type LatestGenre, type SearchSource, type SeriesIntegrityResult, type ChapterDto, type DownloadMissingResultDto, type RefreshChaptersResultDto } from '@/lib/api/types';

export const seriesService = {
  /**
   * Add series with full details to the library
   */
  async addSeries(augmentedResponse: AugmentedResponse): Promise<{ id: string }> {
    return apiClient.post<{ id: string }>('/api/series', augmentedResponse);
  },

  /**
   * Get library series (now returns SeriesInfo[])
   */
  async getLibrary(): Promise<SeriesInfo[]> {
    return apiClient.get<SeriesInfo[]>('/api/series/library');
  },
  /**
   * Get individual series by ID with extended information
   */
  async getSeriesById(id: string): Promise<SeriesExtendedInfo> {
    return apiClient.get<SeriesExtendedInfo>(`/api/series?id=${id}`);
  },

  /**
   * Get provider match information by provider ID
   */
  async getMatch(providerId: string): Promise<ProviderMatch | null> {
    return apiClient.get<ProviderMatch | null>(`/api/series/match/${providerId}`);
  },

  /**
   * Set provider match information
   */
  async setMatch(providerMatch: ProviderMatch): Promise<boolean> {
    return apiClient.post<boolean>('/api/series/match', providerMatch);
  },
  /**
   * Update series information
   */
  async updateSeries(seriesData: SeriesExtendedInfo): Promise<SeriesExtendedInfo> {
    return apiClient.patch<SeriesExtendedInfo>('/api/series', seriesData);
  },

  /**
   * Delete series from the library
   */
  async deleteSeries(id: string, alsoPhysical: boolean = false): Promise<void> {
    const params = new URLSearchParams({
      id: id,
      alsoPhysical: alsoPhysical.toString()
    });
    return apiClient.delete<void>(`/api/series?${params.toString()}`);
  },

  /**
   * Get latest series from cloud providers
   * @param start Starting index for pagination
   * @param count Number of items to return
   * @param sourceId Optional source ID filter
   * @param keyword Optional keyword filter
   * @param genres Optional list of tags (AND semantics — every tag must be present on the title)
   */
  async getLatest(start: number, count: number, sourceId?: string, keyword?: string, genres?: string[]): Promise<LatestSeriesInfo[]> {
    const params = new URLSearchParams({
      start: start.toString(),
      count: count.toString(),
    });

    if (sourceId) {
      params.append('sourceId', sourceId);
    }

    if (keyword) {
      params.append('keyword', keyword);
    }

    if (genres && genres.length > 0) {
      for (const g of genres) {
        const trimmed = g.trim();
        if (trimmed.length > 0) params.append('genre', trimmed);
      }
    }

    return apiClient.get<LatestSeriesInfo[]>(`/api/series/latest?${params.toString()}`);
  },

  /**
   * Get the distinct set of tags/genres available in the cached "Latest"
   * cloud catalogue, with occurrence counts (sorted most-used first).
   */
  async getLatestGenres(): Promise<LatestGenre[]> {
    return apiClient.get<LatestGenre[]>('/api/series/latest/genres');
  },

  /**
   * Get available search sources for series search and filtering
   */
  async getSearchSources(): Promise<SearchSource[]> {
    return apiClient.get<SearchSource[]>('/api/search/sources');
  },

  /**
   * Verify integrity of series files
   */
  async verifyIntegrity(id: string): Promise<SeriesIntegrityResult> {
    const params = new URLSearchParams({
      g: id
    });
    return apiClient.get<SeriesIntegrityResult>(`/api/series/verify?${params.toString()}`);
  },

  /**
   * Cleanup series files with integrity issues
   */
  async cleanupSeries(id: string): Promise<void> {
    const params = new URLSearchParams({
      g: id
    });
    return apiClient.get<void>(`/api/series/cleanup?${params.toString()}`);
  },

  /**
   * Rename all chapter files for a series to use the correct title from the selected title source
   */
  async renameSeriesFiles(id: string): Promise<void> {
    const params = new URLSearchParams({ g: id });
    return apiClient.post<void>(`/api/series/rename?${params.toString()}`, {});
  },

  /**
   * Update all series naming, filenames and ComicInfo.xml with current selected title
   */
  async updateAllSeries(): Promise<void> {
    return apiClient.post<void>('/api/series/update-all', {});
  },

  /**
   * Get all chapters for a series with their download status
   */
  async getChaptersForSeries(id: string): Promise<ChapterDto[]> {
    return apiClient.get<ChapterDto[]>(`/api/series/${id}/chapters`);
  },

  /**
   * Enqueue missing chapter downloads for a series.
   * Omit chapterNumbers (or pass empty) to enqueue all missing chapters.
   */
  async triggerChapterDownloads(id: string, chapterNumbers?: number[]): Promise<DownloadMissingResultDto> {
    const rounded = chapterNumbers?.map(n => Math.round(n * 100) / 100);
    return apiClient.post<DownloadMissingResultDto>(`/api/series/${id}/chapters/download`, { chapterNumbers: rounded });
  },

  /**
   * Force a high-priority GetChapters job per active provider for a series
   */
  async refreshChapters(id: string): Promise<RefreshChaptersResultDto> {
    return apiClient.post<RefreshChaptersResultDto>(`/api/series/${id}/chapters/refresh`, {});
  },
};
