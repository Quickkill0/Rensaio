"use client";

import React, { useState } from 'react';
import Image from 'next/image';
import { useRouter } from 'next/navigation';
import { CheckCircle2, ExternalLink } from 'lucide-react';
import { type SeriesHealth } from '@/lib/api/types';
import { AlertBadge } from '@/components/comp/status/alert-badge';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Card, CardContent } from '@/components/ui/card';
import { formatThumbnailUrl } from '@/lib/utils/thumbnail';
import { seriesService } from '@/lib/api/services/seriesService';

interface SeriesStatusPanelProps {
  series: SeriesHealth[];
  onClearAlert: (targetType: number, targetId: string) => void;
  canAdmin: boolean;
}

export function SeriesStatusPanel({ series, onClearAlert, canAdmin }: SeriesStatusPanelProps) {
  const router = useRouter();
  // Track local cadence input values per series id
  const [cadenceInputs, setCadenceInputs] = useState<Record<string, string>>({});
  // Track saving state per series id
  const [savingCadence, setSavingCadence] = useState<Record<string, boolean>>({});

  if (series.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-muted-foreground">
        <CheckCircle2 className="h-12 w-12 text-green-500 mb-4" />
        <p className="text-lg font-medium">All series are healthy</p>
        <p className="text-sm">No series alerts at this time</p>
      </div>
    );
  }

  // Sort: Red first, then Yellow
  const sorted = [...series].sort((a, b) => a.level - b.level);

  const handleSeriesClick = (seriesId: string) => {
    router.push(`/library/series?id=${seriesId}`);
  };

  const handleSaveCadence = async (seriesId: string) => {
    const input = cadenceInputs[seriesId]?.trim() ?? '';
    const parsed = input ? parseInt(input, 10) : null;

    if (parsed !== null && (isNaN(parsed) || parsed <= 0)) return;

    setSavingCadence(prev => ({ ...prev, [seriesId]: true }));
    try {
      await seriesService.setCadence(seriesId, parsed);
      // Clear the input after successful save
      setCadenceInputs(prev => ({ ...prev, [seriesId]: '' }));
      window.location.reload();
    } catch {
      // Error handled silently
    } finally {
      setSavingCadence(prev => ({ ...prev, [seriesId]: false }));
    }
  };

  return (
    <div className="space-y-2">
      {sorted.map((s) => {
        const currentInput = cadenceInputs[s.id] ?? '';
        const currentValue = currentInput !== ''
          ? parseInt(currentInput, 10)
          : s.releaseCadenceDays;
        const hasValidInput = currentInput === ''
          ? false
          : !isNaN(parseInt(currentInput, 10)) && parseInt(currentInput, 10) > 0;
        const isSaving = savingCadence[s.id] ?? false;

        return (
        <Card key={s.id} className={s.level === 2 ? "border-red-300" : "border-yellow-300"}>
          <CardContent className="py-3 px-4">
            <div className="flex items-center gap-3">
              {/* Thumbnail on the left */}
              <div className="relative flex-shrink-0">
                <Image
                  src={formatThumbnailUrl(s.thumbnailUrl)}
                  alt={s.title}
                  width={48}
                  height={64}
                  className="rounded-md object-cover"
                  onError={(e) => {
                    const target = e.target as HTMLImageElement;
                    target.src = '/rensaio.png';
                  }}
                />
              </div>

              {/* Content area */}
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  {/* Status icon in front of title */}
                  <AlertBadge level={s.level} />
                  {/* Title with link icon at the end */}
                  <span
                    className="text-sm font-medium truncate cursor-pointer hover:text-primary transition-colors flex items-center gap-1"
                    onClick={() => handleSeriesClick(s.id)}
                    title={`View ${s.title} details`}
                  >
                    {s.title}
                    <ExternalLink className="h-3 w-3 shrink-0 text-muted-foreground hover:text-primary" />
                  </span>
                </div>
                <p className="text-xs text-muted-foreground truncate mt-0.5">{s.message}</p>
                {s.providers.length > 0 && (
                  <div className="flex flex-wrap gap-1 mt-1.5">
                    {s.providers.map((p) => (
                      <Badge key={p.providerId} variant="secondary" className="text-xs">
                        {p.providerName} ({p.language})
                      </Badge>
                    ))}
                  </div>
                )}
              </div>

              {/* Actions on the right — two groups: cadence edit (top) + dismiss (bottom) */}
              <div className="flex flex-col items-end gap-2 shrink-0">
                {/* Cadence edit group: label + input + save button */}
                {canAdmin && (
                <div className="flex items-center gap-1.5">
                  <span className="text-xs text-muted-foreground whitespace-nowrap">Cadence:</span>
                  <Input
                    type="number"
                    min="1"
                    step="1"
                    placeholder={s.releaseCadenceDays?.toString() ?? 'auto'}
                    value={cadenceInputs[s.id] ?? ''}
                    onChange={(e) => setCadenceInputs(prev => ({ ...prev, [s.id]: e.target.value }))}
                    className="h-7 w-16 text-xs text-right font-mono"
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        void handleSaveCadence(s.id);
                      }
                    }}
                  />
                  <Button
                    variant="default"
                    size="sm"
                    className="h-7 text-xs"
                    disabled={!hasValidInput || isSaving}
                    onClick={() => void handleSaveCadence(s.id)}
                  >
                    {isSaving ? '...' : 'Save'}
                  </Button>
                </div>
                )}
                {/* Dismiss button + days badge */}
                <div className="flex items-center gap-2">
                  {s.daysWithoutRelease != null && (
                    <Badge variant="outline" className="text-xs">
                      {s.daysWithoutRelease}d
                    </Badge>
                  )}
                  {canAdmin && (
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-7 text-xs"
                    onClick={() => onClearAlert(0, s.id)}
                  >
                    Dismiss
                  </Button>
                  )}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
        );
      })}
    </div>
  );
}
