import { createServer } from 'node:http';

const port = 58081;

function send(res, status, type, body) {
  res.writeHead(status, { 'Content-Type': type, 'Cache-Control': 'no-store' });
  res.end(body);
}

export async function startFakeSourceProviders() {
  const server = createServer((req, res) => {
    const url = new URL(req.url, `http://${req.headers.host}`);
    if (url.pathname.startsWith('/crossref/works/')) {
      send(res, 200, 'application/json', JSON.stringify({ message: {
        title: ['Fake Article'], author: [{ given: 'Alex', family: 'Source' }],
        'published-print': { 'date-parts': [[2024]] }, 'container-title': ['Fake Journal'],
      } }));
      return;
    }
    if (url.pathname.startsWith('/openlibrary/isbn/')) {
      send(res, 200, 'application/json', JSON.stringify({ title: 'Fake ISBN Book', publish_date: '2020', publishers: ['Fake Press'], authors: [{ name: 'Alex Source' }] }));
      return;
    }
    if (url.pathname === '/oembed') {
      send(res, 200, 'application/json', JSON.stringify({ title: 'Fake Video', author_name: 'Fake Channel' }));
      return;
    }
    if (url.pathname.startsWith('/unpaywall/')) {
      send(res, 200, 'application/json', JSON.stringify({ is_oa: false, best_oa_location: null }));
      return;
    }
    if (url.pathname === '/wayback/available') {
      const target = url.searchParams.get('url') || 'https://example.test/';
      send(res, 200, 'application/json', JSON.stringify({ archived_snapshots: {
        closest: { url: `https://web.archive.org/web/20261116090000/${target}`, status: '200', available: true },
      } }));
      return;
    }
    if (url.pathname.startsWith('/wayback/save/')) {
      send(res, 200, 'text/plain', 'snapshot request accepted');
      return;
    }
    if (url.pathname === '/page') {
      send(res, 200, 'text/html; charset=utf-8', `<!doctype html><html><head>
        <title>Fake Article</title><meta property="og:title" content="Fake Article">
        <meta name="citation_author" content="Alex Source"><meta name="datePublished" content="2024-06-01">
        <link rel="license" href="https://creativecommons.org/licenses/by/4.0/">
        </head><body><main><h1>Fake Article</h1><p>Evidence from the licensed page.</p>
        <script>never archive this text</script></main></body></html>`);
      return;
    }
    if (url.pathname === '/page-no-license') {
      send(res, 200, 'text/html; charset=utf-8', '<html><head><title>Page without license</title></head><body><p>Link only.</p></body></html>');
      return;
    }
    send(res, 404, 'text/plain', 'not found');
  });
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(port, '127.0.0.1', resolve);
  });
  return { close: () => new Promise(done => server.close(done)) };
}
