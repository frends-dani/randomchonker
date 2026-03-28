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
        // testDir is tests/RandomChonker.Tests/bin/Debug/net9.0 → walk up to repo root
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
}
