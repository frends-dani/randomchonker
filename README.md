# randomchonker

A single-file fullscreen slideshow of fat cats from [r/chonkers](https://www.reddit.com/r/chonkers/). Meant to run on a dedicated display with nothing but a browser.

![QR code](assets/qrcode.png)

## Usage

Open `index.html` directly in Firefox — no server needed. Chrome blocks cross-origin requests from `file://` URLs, so if you're using Chrome you'll need a local server:

```bash
python3 -m http.server 8080
# then open http://localhost:8080
```

Press `F` to go fullscreen.

## Controls

| Input | Action |
|---|---|
| Click / tap | Pause and show QR code for the current post |
| Click / tap again | Resume slideshow |
| Space / Arrow keys / Enter | Skip to next photo (resumes if paused) |
| `F` | Toggle fullscreen |

## Features

- **Black background at all times** — no white flash during transitions, even while loading
- **Crossfade** between photos
- **Deduplication** — tracks the last 400 seen post IDs in `localStorage` and prefers unseen ones; resets automatically after a full loop
- **Pagination** — fetches more posts from Reddit as it runs low on unseen ones
- **QR code overlay** — pause shows a QR code linking to the original Reddit post, plus a "next photo" button
- **No dependencies, no build step** — just one HTML file

## Configuration

At the top of the `<script>` block:

```js
const DURATION  = 120_000; // ms per photo (default: 2 minutes)
const MAX_SEEN  = 400;     // how many post IDs to remember across sessions
const SUBREDDIT = 'chonkers';
```
