using EducationKeeptrack.Reporting.Application;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace EducationKeeptrack.Reporting.Infrastructure;

public sealed class ChromiumPdfRenderer(IOptions<ReportingOptions> options) : IReportPdfRenderer, IAsyncDisposable
{
    private readonly SemaphoreSlim startup = new(1, 1);
    private readonly SemaphoreSlim slots = new(Math.Clamp(options.Value.RenderConcurrency, 1, 8));
    private IPlaywright? playwright;
    private IBrowser? browser;
    public async Task<byte[]> RenderAsync(string html, CancellationToken ct)
    {
        await slots.WaitAsync(ct);
        try
        {
            await startup.WaitAsync(ct);
            try { if (browser is null || !browser.IsConnected) { playwright ??= await Playwright.CreateAsync(); browser = await playwright.Chromium.LaunchAsync(new() { Headless = true }); } }
            finally { startup.Release(); }
            await using var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false, ServiceWorkers = ServiceWorkerPolicy.Block });
            await context.RouteAsync("**/*", route => route.AbortAsync());
            var page = await context.NewPageAsync(); page.SetDefaultTimeout(Math.Clamp(options.Value.RenderTimeoutSeconds, 5, 120) * 1000);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.RenderTimeoutSeconds, 5, 120)));
            try
            {
                await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.Load }).WaitAsync(timeout.Token);
                await page.EvaluateAsync("async () => { await document.fonts.ready; await Promise.all(Array.from(document.images).map(i => i.decode().catch(() => {}))); }").WaitAsync(timeout.Token);
                return await page.PdfAsync(new() { Format = "A4", PrintBackground = true, DisplayHeaderFooter = true, HeaderTemplate = "<div></div>",
                    FooterTemplate = "<div style='font-size:9px;width:100%;text-align:center'><span class='pageNumber'></span> / <span class='totalPages'></span></div>",
                    Margin = new() { Top = "14mm", Bottom = "16mm", Left = "14mm", Right = "14mm" } }).WaitAsync(timeout.Token);
            }
            finally { await context.CloseAsync(); }
        }
        catch (PlaywrightException e) { throw new ReportRenderException("Không thể dựng PDF. Kiểm tra Chromium và thử lại.", e); }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested) { throw new ReportRenderException("Dựng PDF quá thời gian. Vui lòng thử lại.", e); }
        finally { slots.Release(); }
    }
    public async ValueTask DisposeAsync() { if (browser is not null) await browser.DisposeAsync(); playwright?.Dispose(); startup.Dispose(); slots.Dispose(); }
}
