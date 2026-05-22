import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { seriesService } from '@/lib/api/services/seriesService';
import { type FullSeries, type SeriesInfo, type SeriesExtendedInfo, type ProviderMatch, type AugmentedResponse, type LatestSeriesInfo, type LatestGenre, type SearchSource, type SeriesIntegrityResult, type ChapterDto, ChapterDownloadStatus } from '@/lib/api/types';

/**
 * Hook to get available search sources (for search and filtering)
 */
export const useSearchSources = () => {
  return useQuery({
    queryKey: ['search', 'sources'],
    queryFn: () => seriesService.getSearchSources(),
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
};

/**
 * Hook to add series with full details to the library
 */
export const useAddSeries = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (augmentedResponse: AugmentedResponse) => seriesService.addSeries(augmentedResponse),
    onSuccess: () => {
      // Invalidate library query to refetch
      void queryClient.invalidateQueries({ queryKey: ['series', 'library'] });
    },
  });
};

/**
 * Hook to get library series (now returns SeriesInfo[])
 */
export const useLibrary = () => {
  return useQuery<SeriesInfo[]>({
    queryKey: ['series', 'library'],
    queryFn: () => seriesService.getLibrary(),
    staleTime: 30 * 1000, // 30 seconds - keep data fresh since series status changes affect tab counts
    refetchOnWindowFocus: true, // Refetch when user returns to the library page
  });
};

/**
 * Hook to get individual series by ID with extended information
 */
export const useSeriesById = (id: string, enabled = true) => {
  return useQuery<SeriesExtendedInfo>({
    queryKey: ['series', 'detail', id],
    queryFn: () => seriesService.getSeriesById(id),
    enabled: enabled && !!id,
    staleTime: 0, // Always refetch on navigation to avoid stale data race conditions
    retry: 3, // More retries for transient failures during client-side navigation
  });
};


/**
 * Hook to get provider match information by provider ID
 */
export const useProviderMatch = (providerId: string, enabled = true) => {
  return useQuery<ProviderMatch | null>({
    queryKey: ['series', 'match', providerId],
    queryFn: () => seriesService.getMatch(providerId),
    enabled: enabled && !!providerId,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
};

/**
 * Hook to set provider match information
 */
export const useSetProviderMatch = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (providerMatch: ProviderMatch) => seriesService.setMatch(providerMatch),
    onSuccess: (_, variables) => {
      // Invalidate the specific provider match query
      void queryClient.invalidateQueries({ 
        queryKey: ['series', 'match', variables.id] 
      });
      // Also invalidate series details which might contain match information
      void queryClient.invalidateQueries({ 
        queryKey: ['series', 'detail'] 
      });
    },
  });
};

/**
 * Hook to get latest series from cloud providers
 * @param start Starting index for pagination
 * @param count Number of items to return
 * @param sourceId Optional source ID filter
 * @param keyword Optional keyword filter
 * @param genres Optional tag filter (titles must carry every selected tag)
 * @param enabled Whether the query should be enabled
 */
export const useLatest = (
  start: number,
  count: number,
  sourceId?: string,
  keyword?: string,
  genres?: string[],
  enabled = true
) => {
  // Normalize to a stable, sorted, lowercase signature so query-cache keys
  // don't churn on chip-order changes or casing differences.
  const genreKey = (genres ?? [])
    .map((g) => g.trim().toLowerCase())
    .filter((g) => g.length > 0)
    .sort();

  return useQuery<LatestSeriesInfo[]>({
    queryKey: ['series', 'latest', start, count, sourceId, keyword, genreKey],
    queryFn: () => seriesService.getLatest(start, count, sourceId, keyword, genres),
    enabled,
    staleTime: 2 * 60 * 1000, // 2 minutes - latest content changes frequently
    refetchInterval: 5 * 60 * 1000, // Auto-refetch every 5 minutes for fresh content
  });
};

/**
 * Hook to get the list of available tags/genres for the browse screen filter.
 * Cached for 5 minutes — the underlying server-side aggregation also caches.
 */
export const useLatestGenres = (enabled = true) => {
  return useQuery<LatestGenre[]>({
    queryKey: ['series', 'latest', 'genres'],
    queryFn: () => seriesService.getLatestGenres(),
    enabled,
    staleTime: 5 * 60 * 1000,
  });
};

/**
 * Hook to update series information
 */
export const useUpdateSeries = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (seriesData: SeriesExtendedInfo) => seriesService.updateSeries(seriesData),
    onSuccess: (updatedSeries) => {
      // Update the specific series in the cache
      queryClient.setQueryData(['series', 'detail', updatedSeries.id], updatedSeries);
      // Invalidate library query to refetch
      void queryClient.invalidateQueries({ queryKey: ['series', 'library'] });
    },
  });
};

/**
 * Hook to delete series from the library
 */
export const useDeleteSeries = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ id, alsoPhysical }: { id: string; alsoPhysical: boolean }) => 
      seriesService.deleteSeries(id, alsoPhysical),
    onSuccess: (_, variables) => {
      // Remove the specific series from the cache
      queryClient.removeQueries({ queryKey: ['series', 'detail', variables.id] });
      // Invalidate library query to refetch
      void queryClient.invalidateQueries({ queryKey: ['series', 'library'] });
    },
  });
};

/**
 * Hook to verify series integrity
 */
export const useVerifyIntegrity = () => {
  return useMutation({
    mutationFn: (id: string) => seriesService.verifyIntegrity(id),
  });
};

/**
 * Hook to cleanup series files with integrity issues
 */
export const useCleanupSeries = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (id: string) => seriesService.cleanupSeries(id),
    onSuccess: (_, id) => {
      // Invalidate the specific series detail to refetch updated data
      void queryClient.invalidateQueries({ queryKey: ['series', 'detail', id] });
      // Also invalidate library query in case this affects the series list
      void queryClient.invalidateQueries({ queryKey: ['series', 'library'] });
    },
  });
};

/**
 * Hook to rename series files to use the correct selected title
 */
export const useRenameSeriesFiles = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => seriesService.renameSeriesFiles(id),
    onSuccess: (_, id) => {
      void queryClient.invalidateQueries({ queryKey: ['series', 'detail', id] });
    },
  });
};

/**
 * Hook to update all series naming, filenames and ComicInfo.xml
 */
export const useUpdateAllSeries = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => seriesService.updateAllSeries(),
    onSuccess: () => {
      // Invalidate library query to refetch updated data
      void queryClient.invalidateQueries({ queryKey: ['series', 'library'] });
    },
  });
};

/**
 * Hook to fetch all chapters for a series with their download status.
 * Automatically polls every 10s while any chapter is Queued.
 */
export function useChaptersForSeries(seriesId: string | undefined, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: ['series', seriesId, 'chapters'],
    queryFn: () => seriesService.getChaptersForSeries(seriesId!),
    enabled: !!seriesId && (options?.enabled ?? true),
    refetchInterval: (q) => {
      const data = q.state.data as ChapterDto[] | undefined;
      const hasQueued = data?.some(c => c.status === ChapterDownloadStatus.Queued) ?? false;
      return hasQueued ? 10_000 : false;
    },
    staleTime: 5_000,
  });
}

/**
 * Hook to enqueue missing chapter downloads. Pass chapterNumbers to target
 * specific chapters, or omit to enqueue all missing chapters.
 */
export function useDownloadMissingChapters(seriesId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (chapterNumbers?: number[]) => seriesService.triggerChapterDownloads(seriesId, chapterNumbers),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['series', seriesId, 'chapters'] });
      void queryClient.invalidateQueries({ queryKey: ['downloads', 'series', seriesId] });
    },
  });
}

/**
 * Hook to force a high-priority GetChapters refresh per active provider.
 */
export function useRefreshChapters(seriesId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => seriesService.refreshChapters(seriesId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['series', seriesId, 'chapters'] });
    },
  });
}
