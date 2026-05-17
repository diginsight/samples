using Cocona;
using Diginsight.Diagnostics;
using Diginsight.Components.Azure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Reflection.Metadata;
using Microsoft.Playwright;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Page2Clipboard;

internal sealed class Executor : IDisposable
{
    private readonly ILogger logger;
    private readonly CosmosClient cosmosClient;
    private readonly Container container;
    private readonly string? file;
    private readonly bool whatIf;
    private readonly int? top;
    private readonly string? transformString = """

        """;

    public Executor(ILogger<Executor> logger)
    {
        this.logger = logger;
        using Activity? activity = Observability.ActivitySource.StartMethodActivity(logger);

    }

    public void Dispose()
    {
        cosmosClient?.Dispose();
    }

    public async Task InvokeAsync(
        [FromService] CoconaAppContext appContext,
        [Option('u')] string url
    )
    {
        using Activity? activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { url });

        try
        {
            using var playwright = await Playwright.CreateAsync();

            // Connect to an existing browser instance running with remote debugging enabled
            // Start Edge with: msedge --remote-debugging-port=9222
            // var browser = await playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222");
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Channel = "msedge",  // Use Edge
                Headless = false     // Visible browser
            });
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1440, Height = 1080 }
            });
            var page = await context.NewPageAsync();
            
            // Set the window size to match viewport
            await page.EvaluateAsync("window.resizeTo(1440, 1080)");

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Click "Accept" on the cookie consent banner if present
            var acceptButton = page.Locator("button:has-text('Accept')");
            if (await acceptButton.CountAsync() > 0 && await acceptButton.First.IsVisibleAsync())
            {
                await acceptButton.First.ClickAsync();
                await page.WaitForTimeoutAsync(500); // Wait for banner to disappear
            }

            // Click "Introduction" link if present
            var nextPageLink = page.Locator("a:has-text('Introduction')");
            if (await nextPageLink.CountAsync() <= 0 || !await nextPageLink.First.IsVisibleAsync())
            {
                logger.LogWarning("Learning Path Introduction was not found");
                await InputUser(page, "Learning Path Introduction was not found. Click 'Exit' to continue.", "Exit");
                return;
            }
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var pngBytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Type = ScreenshotType.Png });
            using (var ms = new MemoryStream(pngBytes))
            {
                using (var image = Image.FromStream(ms))
                {
                    var thread = new Thread(() => Clipboard.SetImage(image));
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                }
            }

            // Show overlay with Start button before clicking Introduction
            await InputUser(page, "Learning Path found. Click Start to begin.", "Start");
            await nextPageLink.First.ClickAsync();
            await page.WaitForTimeoutAsync(500); // Wait for navigation


            // Loop through pages taking screenshots
            var pageNumber = 1;
            while (true)
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Scroll through the page to trigger lazy loading
                await page.EvaluateAsync(@"
                    async () => {
                        await new Promise((resolve) => {
                            let totalHeight = 0;
                            const distance = 300;
                            const timer = setInterval(() => {
                                window.scrollBy(0, distance);
                                totalHeight += distance;
                                if (totalHeight >= document.body.scrollHeight) {
                                    clearInterval(timer);
                                    window.scrollTo(0, 0); // Scroll back to top
                                    resolve();
                                }
                            }, 100);
                        });
                    }
                ");
                await page.WaitForTimeoutAsync(500); // Wait for any final lazy-loaded content

                // Take a screenshot of the current page
                var screenshotBytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Type = ScreenshotType.Png });
                using var ms = new MemoryStream(screenshotBytes);
                using var image = Image.FromStream(ms);
                var thread = new Thread(() => Clipboard.SetImage(image));
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                var goBackToFinishButton = page.Locator("button:has-text('Go back to finish'), a:has-text('Go back to finish')");
                var nextPageButton = page.Locator("button:has-text('Next'), a:has-text('Next')");

                var hasGoBackButton = await goBackToFinishButton.CountAsync() > 0 && await goBackToFinishButton.First.IsVisibleAsync();
                var hasNextButton = await nextPageButton.CountAsync() > 0 && await nextPageButton.First.IsVisibleAsync();

                if (hasGoBackButton && !hasNextButton)
                {
                    await InputUser(page, $"All {pageNumber} pages copied! Learning path complete.", "Exit");
                    logger.LogInformation($"Learning path complete. {pageNumber} pages copied.");
                    break;
                }

                if (hasNextButton)
                {
                    // Show overlay message with Next button
                    await InputUser(page, $"Page {pageNumber} copied", "Next");

                    // Click Next button
                    await nextPageButton.First.ClickAsync();
                    await page.WaitForTimeoutAsync(500);
                    pageNumber++;
                }
                else
                {
                    // No Next button and no Go back to finish - unexpected state
                    logger.LogWarning("Neither Next nor Go back to finish button found");
                    await InputUser(page, "Navigation buttons not found. Stopping.", "Exit");
                    break;
                }
            }
        }
        catch (Exception ex) { logger.LogError(ex, $"'{ex.GetType().Name}': {ex.Message}", ex); }
    }

    private async Task InputUser(IPage page, string message, string buttonText)
    {
        // Escape quotes for JavaScript
        var escapedMessage = message.Replace("'", "\\'");
        var escapedButtonText = buttonText.Replace("'", "\\'");

        await page.EvaluateAsync($@"
            window._overlayClicked = false;
            const overlay = document.createElement('div');
            overlay.id = 'copiedOverlay';
            overlay.style.cssText = 'position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); display: flex; justify-content: center; align-items: center; z-index: 999999;';
            const msg = document.createElement('div');
            msg.style.cssText = 'background: #fff; padding: 40px 60px; border-radius: 10px; font-size: 24px; font-weight: bold; color: #1976d2; text-align: center; box-shadow: 0 4px 20px rgba(0,0,0,0.3); display: flex; flex-direction: column; gap: 20px;';
            const text = document.createElement('span');
            text.textContent = '{escapedMessage}';
            const btn = document.createElement('button');
            btn.id = 'overlayNextBtn';
            btn.textContent = '{escapedButtonText}';
            btn.style.cssText = 'padding: 12px 40px; font-size: 18px; font-weight: bold; background: #1976d2; color: white; border: none; border-radius: 5px; cursor: pointer;';
            btn.addEventListener('click', () => {{ window._overlayClicked = true; }});
            msg.appendChild(text);
            msg.appendChild(btn);
            overlay.appendChild(msg);
            document.body.appendChild(overlay);
        ");

        // Wait for user to click the button on the overlay
        await page.WaitForFunctionAsync("() => window._overlayClicked === true");

        // Remove overlay after user clicked
        await page.EvaluateAsync("document.getElementById('copiedOverlay')?.remove();");
    }

}
