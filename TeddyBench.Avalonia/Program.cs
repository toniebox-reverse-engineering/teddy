using Avalonia;
using System;

namespace TeddyBench.Avalonia
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        /* also used by the Avalonia previewer */
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
        }
    }

    /// <summary>Version information of this build (from git, via GitInfo).</summary>
    public static class AppVersion
    {
        /// <summary>e.g. "1.4.2-master+abc1234" (",dirty" for uncommitted changes).</summary>
        public static string Display =>
            ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.SemVer.Patch +
            "-" + ThisAssembly.Git.Branch + "+" + ThisAssembly.Git.Commit + (ThisAssembly.Git.IsDirty ? ",dirty" : "");

        /// <summary>Short version for the about box.</summary>
        public static string Short => ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.SemVer.Patch;

        /// <summary>Last git tag, compared with the latest GitHub release.</summary>
        public static string BaseTag => ThisAssembly.Git.BaseTag;
    }
}
