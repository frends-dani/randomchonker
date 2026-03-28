# randomchonker

<p align="center"><img src="assets/logo.png" width="298" alt="logo"></p>

A single-file fullscreen slideshow of fat cats from [r/chonkers](https://www.reddit.com/r/chonkers/). Meant to run on a dedicated display with nothing but a browser.

## Features

- **Black background at all times** — no white flash during transitions, even while loading
- **Crossfade** between photos
- **Deduplication** — tracks the last 400 seen post IDs in `localStorage` and prefers unseen ones; resets automatically after a full loop
- **Pagination** — fetches more posts from Reddit as it runs low on unseen ones
- **QR code overlay** — pause shows a QR code linking to the original Reddit post, plus a "next photo" button
- **No dependencies, no build step** — just one HTML file

## Usage

Clone the repo and open `index.html` in any browser — no server needed. Press `F` to go fullscreen.

Or just paste one of these into a terminal and go:

**bash / fish (Linux/macOS)**
```bash
curl -sL https://raw.githubusercontent.com/frends-dani/randomchonker/main/index.html -o $HOME/chonkers.html && firefox $HOME/chonkers.html 2>/dev/null || chromium $HOME/chonkers.html 2>/dev/null || google-chrome $HOME/chonkers.html 2>/dev/null || open $HOME/chonkers.html 2>/dev/null
```

**PowerShell (Windows)**
```powershell
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/frends-dani/randomchonker/main/index.html" -OutFile "$env:TEMP\chonkers.html"; Start-Process "$env:TEMP\chonkers.html"
```

## Controls

| Input | Action |
|---|---|
| Click / tap | Pause and show QR code for the current post |
| Click / tap again | Resume slideshow |
| Space / Arrow keys / Enter | Skip to next photo (resumes if paused) |
| `F` | Toggle fullscreen |

![QR code](assets/qrcode.png)

*Click (or tap) anywhere on the photo to pause and show a QR code linking to the original Reddit post of the current chonker.*

## Branch policy

`main` is the release branch — everything on it should be fully working and usable. All changes should go through a pull request with passing tests before merging.

## Configuration

At the top of the `<script>` block:

```js
const DURATION  = 120_000; // ms per photo (default: 2 minutes)
const MAX_SEEN  = 400;     // how many post IDs to remember across sessions
const SUBREDDIT = 'chonkers';
```
