import { buildTutorialVideoConfig } from './tutorial-video.config';

describe('tutorialVideoConfig', () => {
  it('should build youtube embed and watch URLs from video id', () => {
    const config = buildTutorialVideoConfig('abc123_xyz');

    expect(config.youtubeVideoId).toBe('abc123_xyz');
    expect(config.embedUrl).toBe('https://www.youtube-nocookie.com/embed/abc123_xyz?rel=0&modestbranding=1');
    expect(config.watchUrl).toBe('https://www.youtube.com/watch?v=abc123_xyz');
  });
});
