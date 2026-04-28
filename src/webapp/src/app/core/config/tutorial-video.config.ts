export type TutorialVideoSource = 'home' | 'exercise_help_icon' | 'exercise_tour_final_step';

export interface TutorialVideoConfig {
  youtubeVideoId: string;
  embedUrl: string;
  watchUrl: string;
  title: string;
}

export function buildTutorialVideoConfig(youtubeVideoId: string): TutorialVideoConfig {
  const normalizedVideoId = youtubeVideoId.trim();

  return {
    youtubeVideoId: normalizedVideoId,
    embedUrl: `https://www.youtube-nocookie.com/embed/${encodeURIComponent(normalizedVideoId)}?rel=0&modestbranding=1`,
    watchUrl: `https://www.youtube.com/watch?v=${encodeURIComponent(normalizedVideoId)}`,
    title: 'How to use Listen and Write'
  };
}

// Replace this value with your recorded tutorial video id (YouTube "v" parameter).
const youtubeVideoId = 'UJp1qR2oN38';

export const tutorialVideoConfig = buildTutorialVideoConfig(youtubeVideoId);
