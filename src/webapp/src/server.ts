import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import compression from 'compression';
import express from 'express';
import { join } from 'node:path';
import { environment } from './enviroments/enviroment';

const browserDistFolder = join(import.meta.dirname, '../browser');

const apiUrl = environment.apiUrl;

console.log(`[Server] Using API URL: ${apiUrl}`);

const app = express();
const angularApp = new AngularNodeAppEngine();

app.use(compression());

/**
 * Example Express Rest API endpoints can be defined here.
 * Uncomment and define endpoints as necessary.
 *
 * Example:
 * ```ts
 * app.get('/api/{*splat}', (req, res) => {
 *   // Handle API request
 * });
 * ```
 */

/**
 * Server-side API client for fetching exercises
 * Uses the same endpoint structure as PropositionsService but with fetch API
 */
async function fetchExercises(
  pageNumber: number,
  pageSize: number,
  topic?: string,
  level?: string,
  sortBy: string = 'newest'
): Promise<{ items: any[]; hasNext: boolean }> {
  const params = new URLSearchParams({
    pageNumber: pageNumber.toString(),
    pageSize: pageSize.toString(),
    sortBy: sortBy
  });

  if (topic) params.append('topic', topic);
  if (level) params.append('level', level);

  const response = await fetch(`${apiUrl}/api/Proposition/exercises?${params.toString()}`);
  
  if (!response.ok) {
    throw new Error(`API request failed: ${response.status}`);
  }

  return response.json();
}

/**
 * Sitemap endpoint - generates dynamic sitemap based on available exercises
 * Fetches all exercises from the API and includes them in the sitemap
 */
app.get('/sitemap.xml', async (req, res) => {
  try {
    const baseUrl = req.protocol + '://' + req.get('host');
    
    // Start building sitemap with static pages
    let sitemap = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>${baseUrl}/</loc>
    <changefreq>weekly</changefreq>
    <priority>1.0</priority>
    <lastmod>${new Date().toISOString().split('T')[0]}</lastmod>
  </url>
  <url>
    <loc>${baseUrl}/exercises</loc>
    <changefreq>daily</changefreq>
    <priority>0.9</priority>
    <lastmod>${new Date().toISOString().split('T')[0]}</lastmod>
  </url>
  <url>
    <loc>${baseUrl}/about</loc>
    <changefreq>monthly</changefreq>
    <priority>0.5</priority>
    <lastmod>${new Date().toISOString().split('T')[0]}</lastmod>
  </url>`;

    // Fetch all exercises from API using our server-side client
    try {
      const pageSize = 100;
      let currentPage = 1;
      let hasMore = true;

      while (hasMore) {
        const data = await fetchExercises(currentPage, pageSize);
        const items = data.items || [];
        
        // Add each exercise to sitemap
        items.forEach((exercise: any) => {
          const publishDate = exercise.publishedOn 
            ? new Date(exercise.publishedOn).toISOString().split('T')[0]
            : new Date().toISOString().split('T')[0];

          sitemap += `
  <url>
    <loc>${baseUrl}/english-writing-exercise/${exercise.id}</loc>
    <changefreq>weekly</changefreq>
    <priority>0.8</priority>
    <lastmod>${publishDate}</lastmod>
  </url>`;
        });

        // Check if there are more pages
        hasMore = data.hasNext === true;
        currentPage++;

        // Safety limit to prevent infinite loops
        if (currentPage > 100) {
          console.warn('Sitemap generation: Reached maximum page limit');
          break;
        }
      }

      console.log(`Sitemap generated with exercises from ${currentPage - 1} page(s)`);
    } catch (apiError) {
      console.error('Error fetching exercises for sitemap:', apiError);
      // Continue with static pages only if API fails
    }

    // Close sitemap XML
    sitemap += '\n</urlset>';

    res.header('Content-Type', 'application/xml');
    res.header('Cache-Control', 'public, max-age=3600'); // Cache for 1 hour
    res.send(sitemap);
  } catch (error) {
    console.error('Error generating sitemap:', error);
    res.status(500).send('Error generating sitemap');
  }
});

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) =>
      response ? writeResponseToNodeResponse(response, res) : next(),
    )
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
