using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace RandomChonker.Tests;

[TestFixture]
public class ChonkerTests : PageTest
{
    private string _htmlPath = null!;
    private string _fixturesPath = null!;

    [SetUp]
    public async Task SetUp()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        _fixturesPath = Path.Combine(testDir, "fixtures");
        // testDir is tests/RandomChonker.Tests/bin/Debug/net10.0 → walk up to repo root
        _htmlPath = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "index.html"));

        // Mock Reddit API — no real requests
        await Page.RouteAsync("**/www.reddit.com/**", async route =>
        {
            var json = await File.ReadAllTextAsync(Path.Combine(_fixturesPath, "reddit_response.json"));
            await route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = json
            });
        });

        // Mock QR code API
        await Page.RouteAsync("**/api.qrserver.com/**", async route =>
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(_fixturesPath, "fake_image.png"));
            await route.FulfillAsync(new()
            {
                ContentType = "image/png",
                BodyBytes = bytes
            });
        });

        // Mock all Reddit-hosted images
        await Page.RouteAsync("**/i.redd.it/**", async route =>
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(_fixturesPath, "fake_image.png"));
            await route.FulfillAsync(new()
            {
                ContentType = "image/jpeg",
                BodyBytes = bytes
            });
        });

        await Page.GotoAsync($"file://{_htmlPath}");

        // Wait for the first image to appear (frame-b gets the first image since current starts as 'a')
        await Expect(Page.Locator("#frame-b.visible")).ToHaveCountAsync(1, new() { Timeout = 10_000 });
    }

    [Test]
    public async Task FirstImageLoadsAndDisplays()
    {
        // frame-b should be visible with a loaded image
        var img = Page.Locator("#frame-b.visible img");
        await Expect(img).ToHaveCountAsync(1);

        var src = await img.GetAttributeAsync("src");
        Assert.That(src, Does.Contain("i.redd.it"));
    }

    [Test]
    public async Task ClickPausesAndShowsOverlay()
    {
        await Page.ClickAsync("body");

        var overlay = Page.Locator("#qr-overlay.visible");
        await Expect(overlay).ToHaveCountAsync(1);
    }

    [Test]
    public async Task OverlayShowsPostTitle()
    {
        await Page.ClickAsync("body");

        var title = await Page.Locator("#qr-title").TextContentAsync();
        // Title should be one of our fixture post titles
        var validTitles = new[]
        {
            "Absolute unit of a cat",
            "My neighbor's chonky boy",
            "He sits where he fits",
            "Round boi on a chair"
        };
        Assert.That(validTitles, Does.Contain(title));
    }

    [Test]
    public async Task ClickAgainResumes()
    {
        // Pause
        await Page.ClickAsync("body");
        await Expect(Page.Locator("#qr-overlay.visible")).ToHaveCountAsync(1);

        // Resume
        await Page.ClickAsync("body");
        await Expect(Page.Locator("#qr-overlay.visible")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SpaceKeySkipsToNextPhoto()
    {
        // First image is on frame-b; pressing space should trigger a cycle that shows frame-a
        await Page.Keyboard.PressAsync("Space");

        await Expect(Page.Locator("#frame-a.visible")).ToHaveCountAsync(1, new() { Timeout = 5_000 });
    }

    [Test]
    public async Task ArrowRightSkipsToNextPhoto()
    {
        await Page.Keyboard.PressAsync("ArrowRight");

        await Expect(Page.Locator("#frame-a.visible")).ToHaveCountAsync(1, new() { Timeout = 5_000 });
    }

    [Test]
    public async Task ArrowDownSkipsToNextPhoto()
    {
        await Page.Keyboard.PressAsync("ArrowDown");

        await Expect(Page.Locator("#frame-a.visible")).ToHaveCountAsync(1, new() { Timeout = 5_000 });
    }

    [Test]
    public async Task EnterKeySkipsToNextPhoto()
    {
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#frame-a.visible")).ToHaveCountAsync(1, new() { Timeout = 5_000 });
    }

    [Test]
    public async Task NextPhotoButtonSkipsWhilePaused()
    {
        // Pause first
        await Page.ClickAsync("body");
        await Expect(Page.Locator("#qr-overlay.visible")).ToHaveCountAsync(1);

        // Click "next photo →" button
        await Page.ClickAsync("#qr-next");

        // Overlay should close and next frame should appear
        await Expect(Page.Locator("#qr-overlay.visible")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#frame-a.visible")).ToHaveCountAsync(1, new() { Timeout = 5_000 });
    }

    [Test]
    public async Task ProgressBarIsPresent()
    {
        var progress = Page.Locator("#progress");
        await Expect(progress).ToHaveCountAsync(1);

        // Animation is applied via a double-rAF, so wait for it
        var animation = await Page.EvaluateAsync<string>(
            @"() => new Promise(resolve => {
                const el = document.getElementById('progress');
                const check = () => {
                    const name = getComputedStyle(el).animationName;
                    if (name && name !== 'none') resolve(name);
                    else requestAnimationFrame(check);
                };
                check();
            })");
        Assert.That(animation, Is.EqualTo("progress-tick"));
    }

    [Test]
    public async Task SeenPostsSavedToLocalStorage()
    {
        var seen = await Page.EvaluateAsync<string[]>(
            "JSON.parse(localStorage.getItem('chonker_seen_v1') || '[]')");

        Assert.That(seen, Is.Not.Empty);
        // The seen ID should be one of our fixture posts
        var validIds = new[] { "post_alpha", "post_bravo", "post_charlie", "post_delta" };
        Assert.That(seen, Has.All.Matches<string>(id => validIds.Contains(id)));
    }

    [Test]
    public async Task VideoPostsAreFilteredOut()
    {
        // The fixture includes a video post (post_video_skip). Skip through all posts
        // and verify the video post ID never appears in localStorage.
        for (var i = 0; i < 5; i++)
            await Page.Keyboard.PressAsync("Space");

        // Give the last cycle a moment to complete
        await Page.WaitForTimeoutAsync(500);

        var seen = await Page.EvaluateAsync<string[]>(
            "JSON.parse(localStorage.getItem('chonker_seen_v1') || '[]')");

        Assert.That(seen, Does.Not.Contain("post_video_skip"));
    }

    [Test]
    public async Task PauseStopsProgressBar()
    {
        await Page.ClickAsync("body");

        var playState = await Page.Locator("#progress")
            .EvaluateAsync<string>("el => getComputedStyle(el).animationPlayState");

        Assert.That(playState, Is.EqualTo("paused"));
    }

    [Test]
    public async Task CursorHiddenDuringPlayback()
    {
        var cursor = await Page.EvaluateAsync<string>("getComputedStyle(document.body).cursor");
        Assert.That(cursor, Is.EqualTo("none"));
    }

    [Test]
    public async Task CursorVisibleWhenPaused()
    {
        await Page.ClickAsync("body");

        var cursor = await Page.EvaluateAsync<string>("document.body.style.cursor");
        Assert.That(cursor, Is.EqualTo("default"));
    }

    [Test]
    public async Task CursorHiddenAgainAfterResume()
    {
        await Page.ClickAsync("body");
        await Page.ClickAsync("body");

        var cursor = await Page.EvaluateAsync<string>("document.body.style.cursor");
        Assert.That(cursor, Is.EqualTo("none"));
    }

    [Test]
    public async Task ImageLoadFailureSkipsToNext()
    {
        // Unroute the image mock and replace with one that fails for a specific image
        await Page.UnrouteAsync("**/i.redd.it/**");
        var failedOnce = false;
        await Page.RouteAsync("**/i.redd.it/**", async route =>
        {
            if (!failedOnce)
            {
                failedOnce = true;
                await route.AbortAsync();
                return;
            }
            var bytes = await File.ReadAllBytesAsync(Path.Combine(_fixturesPath, "fake_image.png"));
            await route.FulfillAsync(new()
            {
                ContentType = "image/jpeg",
                BodyBytes = bytes
            });
        });

        await Page.Keyboard.PressAsync("Space");

        // Should eventually show a new image despite the first one failing
        await Expect(Page.Locator("#frame-a.visible")).ToHaveCountAsync(1, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task SeenListResetsWhenAllPostsSeen()
    {
        // Mark all fixture posts as seen
        await Page.EvaluateAsync(
            @"localStorage.setItem('chonker_seen_v1',
                JSON.stringify(['post_alpha','post_bravo','post_charlie','post_delta']))");

        // Trigger a skip — should loop around and reset
        await Page.Keyboard.PressAsync("Space");
        await Page.WaitForTimeoutAsync(1000);

        // After reset, the seen list should be short (just the newly picked post)
        var seen = await Page.EvaluateAsync<string[]>(
            "JSON.parse(localStorage.getItem('chonker_seen_v1') || '[]')");

        // Should have been reset (not still 4 old + new ones)
        Assert.That(seen.Length, Is.LessThanOrEqualTo(2));
    }

    [Test]
    public async Task StatusMessageAppearsOnLoopAround()
    {
        // Mark all fixture posts as seen
        await Page.EvaluateAsync(
            @"localStorage.setItem('chonker_seen_v1',
                JSON.stringify(['post_alpha','post_bravo','post_charlie','post_delta']))");

        await Page.Keyboard.PressAsync("Space");

        // The status element should briefly show "looped around"
        var status = Page.Locator("#status.show");
        await Expect(status).ToHaveCountAsync(1, new() { Timeout = 5_000 });

        var text = await Page.Locator("#status").TextContentAsync();
        Assert.That(text, Does.Contain("looped around"));
    }

    [Test]
    public async Task SpaceKeyResumesFromPause()
    {
        // Pause
        await Page.ClickAsync("body");
        await Expect(Page.Locator("#qr-overlay.visible")).ToHaveCountAsync(1);

        // Space should skip (which implicitly resumes)
        await Page.Keyboard.PressAsync("Space");

        await Expect(Page.Locator("#qr-overlay.visible")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#frame-a.visible")).ToHaveCountAsync(1, new() { Timeout = 5_000 });
    }

    [Test]
    public async Task QrOverlayShowsQrImage()
    {
        await Page.ClickAsync("body");

        var qrSrc = await Page.Locator("#qr-img").GetAttributeAsync("src");
        Assert.That(qrSrc, Does.Contain("api.qrserver.com"));
        Assert.That(qrSrc, Does.Contain("reddit.com"));
    }
}

[TestFixture]
public class ChonkerPaginationTests : PageTest
{
    private string _htmlPath = null!;
    private string _fixturesPath = null!;
    private int _requestCount;

    [SetUp]
    public async Task SetUp()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        _fixturesPath = Path.Combine(testDir, "fixtures");
        _htmlPath = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "index.html"));
        _requestCount = 0;

        // First request returns page 1 with an after token, second returns page 2
        await Page.RouteAsync("**/www.reddit.com/**", async route =>
        {
            _requestCount++;
            var file = _requestCount == 1 ? "reddit_response_paginated.json" : "reddit_response_page2.json";
            var json = await File.ReadAllTextAsync(Path.Combine(_fixturesPath, file));
            await route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = json
            });
        });

        await Page.RouteAsync("**/api.qrserver.com/**", async route =>
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(_fixturesPath, "fake_image.png"));
            await route.FulfillAsync(new()
            {
                ContentType = "image/png",
                BodyBytes = bytes
            });
        });

        await Page.RouteAsync("**/i.redd.it/**", async route =>
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(_fixturesPath, "fake_image.png"));
            await route.FulfillAsync(new()
            {
                ContentType = "image/jpeg",
                BodyBytes = bytes
            });
        });

        await Page.GotoAsync($"file://{_htmlPath}");
        await Expect(Page.Locator(".frame.visible")).ToHaveCountAsync(1, new() { Timeout = 10_000 });
    }

    [Test]
    public async Task FetchesNextPageWhenRunningLow()
    {
        // First page has only 1 post. After seeing it, skipping should trigger page 2 fetch.
        await Page.Keyboard.PressAsync("Space");
        await Page.WaitForTimeoutAsync(1000);

        // Page 2 posts should now be in the seen list
        var seen = await Page.EvaluateAsync<string[]>(
            "JSON.parse(localStorage.getItem('chonker_seen_v1') || '[]')");

        var allIds = new[] { "post_alpha", "post_echo", "post_foxtrot" };
        Assert.That(seen, Has.Some.Matches<string>(id => allIds.Contains(id)));
        Assert.That(_requestCount, Is.GreaterThan(1));
    }
}
