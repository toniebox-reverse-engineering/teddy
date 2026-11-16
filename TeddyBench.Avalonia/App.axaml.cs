using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using NAudio.Wave;
using System;
using TeddyBench.Avalonia.Services;
using TeddyBench.Avalonia.ViewModels;
using TeddyBench.Avalonia.Views;
using TeddyBench.Core;

namespace TeddyBench.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                /* NAudio's output is Windows only; elsewhere PCM is piped to pw-play/paplay/aplay */
                PlatformServices.AudioOutputFactory = OperatingSystem.IsWindows()
                    ? () => new WaveOutEvent()
                    : PipeAudioOutput.TryCreateFactory();

                var window = new MainWindow();
                var vm = new MainWindowViewModel(new DialogService(window), new AvaloniaUiDispatcher(), new CoverImages(),
                    MainWindowServices.CreateDefault(AppVersion.Display, AppVersion.BaseTag));

                SafeThread.UnhandledException += vm.ReportException;
                Dispatcher.UIThread.UnhandledException += (s, e) =>
                {
                    vm.ReportException("Main Application Thread", e.Exception);
                    e.Handled = true;
                };

                window.DataContext = vm;
                window.Opened += (s, e) => vm.Start();
                window.Closing += (s, e) => vm.Shutdown();
                vm.CloseRequested += window.Close;
                desktop.MainWindow = window;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
