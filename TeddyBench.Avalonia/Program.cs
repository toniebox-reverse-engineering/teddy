using Avalonia;
using Avalonia.Controls;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TeddyBench.Avalonia;

sealed class Program
{
    private static Mutex? _mutex;
    private const string MutexName = "TeddyBench.Avalonia.SingleInstance";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Check for single instance
        bool createdNew;
        _mutex = new Mutex(true, MutexName, out createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            ShowSingleInstanceDialog();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // On Linux, disable DBus file picker to use GTK file picker instead
        // DBus file picker doesn't support SuggestedStartLocation, but GTK does
        // This allows the auto-open directory feature to navigate to SD cards
        // Note: GTK file picker has a different visual style than the modern DBus picker,
        // but it's the only way to support suggested start locations on Linux
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            builder.With(new X11PlatformOptions
            {
                UseDBusFilePicker = false
            });
        }

        return builder;
    }

    private static void ShowSingleInstanceDialog()
    {
        // Check if we're in a headless environment (no display available)
        var isHeadless = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                         string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

        if (isHeadless)
        {
            // In headless mode, just print to console and exit
            Console.WriteLine("TeddyBench is already running.");
            return;
        }

        // Initialize a minimal Avalonia app to show the message
        try
        {
            var appBuilder = BuildAvaloniaApp();
            var cancellationSource = new System.Threading.CancellationTokenSource();

            appBuilder.AfterSetup(_ =>
            {
                var lifetime = (global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                    Application.Current!.ApplicationLifetime!;

                var dialog = new global::Avalonia.Controls.Window
                {
                    Title = "TeddyBench",
                    Width = 400,
                    Height = 150,
                    CanResize = false,
                    WindowStartupLocation = global::Avalonia.Controls.WindowStartupLocation.CenterScreen,
                    Content = new global::Avalonia.Controls.StackPanel
                    {
                        Margin = new global::Avalonia.Thickness(20),
                        Spacing = 20,
                        Children =
                        {
                            new global::Avalonia.Controls.TextBlock
                            {
                                Text = "TeddyBench is already running.",
                                FontSize = 14,
                                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                            },
                            new global::Avalonia.Controls.Button
                            {
                                Content = "OK",
                                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                                Width = 80
                            }
                        }
                    }
                };

                // Auto-close after 3 seconds (useful for automated tests)
                var autoCloseTimer = new System.Timers.Timer(3000);
                autoCloseTimer.Elapsed += (_, _) =>
                {
                    autoCloseTimer.Stop();
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        dialog.Close();
                        lifetime.Shutdown();
                    });
                };
                autoCloseTimer.Start();

                // Find and wire up the button
                if (dialog.Content is global::Avalonia.Controls.StackPanel panel)
                {
                    foreach (var child in panel.Children)
                    {
                        if (child is global::Avalonia.Controls.Button button)
                        {
                            button.Click += (_, _) =>
                            {
                                autoCloseTimer.Stop();
                                dialog.Close();
                                lifetime.Shutdown();
                            };
                            break;
                        }
                    }
                }

                dialog.Show();
            });

            appBuilder.StartWithClassicDesktopLifetime(new string[0], ShutdownMode.OnExplicitShutdown);
        }
        catch
        {
            // Fallback to console message if dialog fails
            Console.WriteLine("TeddyBench is already running.");
        }
    }
}
