export interface ProgressFeedbackVideoConfig {
  youtubeVideoId: string;
  embedUrl: string;
  watchUrl: string;
  title: string;
}

export function buildProgressFeedbackVideoConfig(youtubeVideoId: string): ProgressFeedbackVideoConfig {
  const normalizedVideoId = youtubeVideoId.trim();
  const encodedVideoId = encodeURIComponent(normalizedVideoId);

  return {
    youtubeVideoId: normalizedVideoId,
    embedUrl: normalizedVideoId
      ? `https://www.youtube-nocookie.com/embed/${encodedVideoId}?rel=0&modestbranding=1`
      : '',
    watchUrl: normalizedVideoId
      ? `https://www.youtube.com/watch?v=${encodedVideoId}`
      : '',
    title: 'A quick note from Matthew'
  };
}

// Replace this value with the YouTube "v" parameter for the progress feedback video.
const youtubeVideoId = 'uLTovUGSteQ';

export const progressFeedbackVideoConfig = buildProgressFeedbackVideoConfig(youtubeVideoId);
