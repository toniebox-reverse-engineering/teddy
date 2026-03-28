using Avalonia;
using Avalonia.Headless;

namespace TeddyBench.Avalonia.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            });
}
