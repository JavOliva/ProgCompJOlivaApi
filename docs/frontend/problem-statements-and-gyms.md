# Problem statements & Codeforces gyms — frontend guide

Two features for the frontend:

1. **View a problem statement** (public — no auth).
2. **Add a Codeforces gym** which imports its problems (admin).

---

## 1. View a problem statement — public

Statements are stored as HTML fragments (with MathJax math) and served without authentication.

### By judge + external id

```
GET /api/problem/statement?judge={judge}&externalId={externalId}
```

- `judge`: `Cses`, `Codeforces`, `AtCoder`, … (the problem's `judge` field).
- `externalId`: the problem's `externalId` field (e.g. CSES `1068`, Codeforces `567665/problem/A`).

### By internal id

```
GET /api/problem/{id}/statement
```

### Response `200`

```json
{
  "judge": "Cses",
  "externalId": "1068",
  "title": "Weird Algorithm",
  "html": "<div class=\"md\"><p>Consider an algorithm … \\(n\\) … \\[ 3 \\rightarrow 10 \\rightarrow 1\\]</p>…</div>"
}
```

- `html` is an **HTML fragment** (a `<div>`). Inject it into the page and run MathJax.
- Math uses **standard MathJax delimiters**: `\( … \)` inline, `\[ … \]` display.
- Image URLs are already **absolute**, so they load directly.

### Response `404`

- Problem not found, **or** the statement hasn't been fetched yet. Statements are populated in the
  background (see §3), so a freshly-added problem may 404 for a short while. The problem
  list/search items expose **`hasStatement`** (boolean) — use it to know whether to show a
  "View statement" button.

### Rendering with MathJax

Load MathJax and configure the delimiters, then typeset the injected HTML:

```html
<script>
  window.MathJax = {
    tex: { inlineMath: [['\\(', '\\)']], displayMath: [['\\[', '\\]']] }
  };
</script>
<script src="https://cdn.jsdelivr.net/npm/mathjax@3/es5/tex-mml-chtml.js" async></script>
```

```js
const res = await fetch(`/api/problem/statement?judge=Cses&externalId=1068`);
if (res.ok) {
  const { html, title } = await res.json();
  container.innerHTML = html;
  await window.MathJax.typesetPromise([container]); // render the math
}
```

---

## 2. Add a Codeforces gym (import its problems) — admin

```
POST /api/codeforces-gym/{gymContestId}/import        (Authorization: Bearer <admin token>)
```

What it does, in one call:
- Registers the gym in the registry (**enabled**, so the solve sync starts tracking it).
- Imports all of the gym's problems via the Codeforces API (idempotent — re-importing only adds
  problems that aren't there yet).

It makes **one rate-limited Codeforces call**, so the request can take a few seconds.

### Response `200`

```json
{
  "gymContestId": 567665,
  "name": "Busqueda Binaria | Dos Punteros",
  "addedProblems": 34,
  "totalProblems": 34,
  "gymWasNew": true
}
```

### Errors
- `400` — gym id isn't a positive integer.
- `401` / `403` — not an admin.
- `502` — Codeforces unreachable / returned an error, or the API key/secret aren't configured
  server-side.

The imported problems show up immediately in `GET /api/problem?judge=Codeforces`, and the gym in
`GET /api/codeforces-gym`. Their **statements** are filled in afterwards by the background worker
(see §3).

---

## 3. How statements get populated (background)

- **CSES**: fetched automatically at startup from the public CSES pages — no configuration needed.
- **Codeforces**: fetched by the background worker, but **only if the server has a
  `Codeforces:SessionCookie` configured** (a logged-in codeforces.com cookie). Codeforces statement
  fetches are batched (a handful per 5-minute cycle) and rate-limited ≥5s apart, so a large gym
  backfills over several minutes.
  > ⚠️ **Caveat:** Codeforces' web pages sit behind **Cloudflare bot protection** (they return
  > `403` even to anonymous requests for *public* problems), and gym pages additionally require
  > login. The scraper sends a browser User-Agent and the configured session cookie, but Cloudflare
  > may still block server-side requests. So Codeforces statements are **best-effort** — if they
  > don't populate, the cookie/Cloudflare is why. CSES statements are unaffected and reliable.

So in the UI: rely on the problem's `hasStatement` flag to decide whether the statement is ready,
and treat `404` from the statement endpoint as "not available yet".
