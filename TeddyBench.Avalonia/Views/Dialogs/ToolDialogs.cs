using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Id3;
using ScottPlot.Avalonia;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using TeddyBench.Core;

namespace TeddyBench.Avalonia.Views.Dialogs
{
    /// <summary>Orders the MP3/Ogg files of a new tonie (initially by ID3 track number).</summary>
    public class TrackSortDialog : DialogBase
    {
        public class Track
        {
            public string File { get; set; }
            public string Id3 { get; set; }
            public int Number { get; set; }
            public string Display => Number.ToString().PadLeft(3) + "   " + Path.GetFileName(File) + (string.IsNullOrEmpty(Id3) ? "" : "   (" + Id3 + ")");
        }

        private readonly ObservableCollection<Track> Tracks = new ObservableCollection<Track>();
        private readonly ListBox List;

        public string[] Result { get; private set; }

        public TrackSortDialog(string[] files) : base("Sort tracks")
        {
            CanResize = true;
            SizeToContent = SizeToContent.Manual;
            Width = 720;
            Height = 460;

            foreach (var t in files.Select(f => new { File = f, Tag = GetTag(f) }).OrderBy(t => t.Tag?.Track.Value ?? int.MaxValue))
            {
                Tracks.Add(new Track { File = t.File, Id3 = t.Tag == null ? null : t.Tag.Artists + " - " + t.Tag.Title });
            }
            Renumber();

            List = new ListBox
            {
                ItemsSource = Tracks,
                SelectionMode = SelectionMode.Multiple,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,Liberation Mono,monospace"),
                ItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<Track>((t, _) => new TextBlock { Text = t?.Display })
            };

            var side = new StackPanel { Spacing = 8, Margin = new Thickness(8, 0, 0, 0) };
            side.Children.Add(MakeButton("Up", () => Move(-1)));
            side.Children.Add(MakeButton("Down", () => Move(1)));
            DockPanel.SetDock(side, Dock.Right);

            var buttons = ButtonRow(MakeButton("OK", () => { Result = Tracks.Select(t => t.File).ToArray(); Close(); }, isDefault: true), MakeButton("Cancel", Close, isCancel: true));
            DockPanel.SetDock(buttons, Dock.Bottom);

            var hint = Text("The tracks are written in this order. Select tracks and move them with Up/Down.");
            hint.HorizontalAlignment = HorizontalAlignment.Left;
            hint.Margin = new Thickness(0, 0, 0, 8);
            DockPanel.SetDock(hint, Dock.Top);

            var panel = new DockPanel { Margin = new Thickness(16) };
            panel.Children.Add(hint);
            panel.Children.Add(buttons);
            panel.Children.Add(side);
            panel.Children.Add(List);
            Content = panel;
        }

        private static Id3Tag GetTag(string file)
        {
            try
            {
                using (var mp3 = new Mp3(file, Mp3Permissions.Read))
                {
                    return mp3.GetAllTags().FirstOrDefault(t => t.Track.IsAssigned);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Move(int direction)
        {
            var selected = List.SelectedItems.Cast<Track>().ToList();
            if (selected.Count == 0)
            {
                return;
            }
            var indices = selected.Select(t => Tracks.IndexOf(t)).OrderBy(i => i).ToList();
            if ((direction < 0 && indices.First() == 0) || (direction > 0 && indices.Last() == Tracks.Count - 1))
            {
                return;
            }

            foreach (int index in direction < 0 ? indices : Enumerable.Reverse(indices))
            {
                Tracks.Move(index, index + direction);
            }
            Renumber();

            /* refresh texts and keep the selection */
            List.ItemsSource = null;
            List.ItemsSource = Tracks;
            foreach (var t in selected)
            {
                List.SelectedItems.Add(t);
            }
        }

        private void Renumber()
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                Tracks[i].Number = i + 1;
            }
        }
    }

    public class AboutDialog : DialogBase
    {
        public AboutDialog(string version) : base("About")
        {
            var panel = new StackPanel { Margin = new Thickness(24), Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
            using (var stream = AssetLoader.Open(new Uri("avares://TeddyBench/Assets/custom.png")))
            {
                panel.Children.Add(new Image { Source = Bitmap.DecodeToWidth(stream, 96), Width = 96, Height = 96 });
            }
            panel.Children.Add(new TextBlock { Text = "TeddyBench", FontSize = 22, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = "v" + version, HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = "by Team RevvoX", HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(Link("Icon source", "https://www.flaticon.com/de/kostenloses-icon/hund_2829818", "https://www.flaticon.com/de/kostenloses-icon/fragen_1923795"));
            panel.Children.Add(Link("visit our telegram channel", "https://t.me/toniebox_reverse_engineering"));
            panel.Children.Add(ButtonRow(MakeButton("OK", Close, isDefault: true, isCancel: true)));
            Content = panel;
        }

        private static Control Link(string text, params string[] urls)
        {
            var button = new Button
            {
                Content = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.Parse("#1E88E5")),
                Padding = new Thickness(4, 0),
                Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand)
            };
            button.Click += (s, e) =>
            {
                foreach (var url in urls)
                {
                    try
                    {
                        PlatformServices.Shell.OpenUrl(url);
                    }
                    catch (Exception)
                    {
                    }
                }
            };
            return button;
        }
    }

    /// <summary>Proxmark3 antenna measurement: LF sweep plot and the HF voltage rating.</summary>
    public class AntennaPlotWindow : DialogBase
    {
        public AntennaPlotWindow(Proxmark3.MeasurementResult result) : base("Proxmark 3 Antenna Plot")
        {
            CanResize = true;
            SizeToContent = SizeToContent.Manual;
            Width = 820;
            Height = 520;

            var plot = new AvaPlot();
            plot.Plot.Add.Scatter(result.GetFrequencieskHz(), result.GetVoltages());
            plot.Plot.XLabel("Frequency [kHz]");
            plot.Plot.YLabel("Amplitude [V]");
            plot.Plot.Title("LF Antenna Plot (no relevance)");
            plot.Plot.Axes.SetLimits(46.0, 600.0, 0, 65.0);
            plot.Refresh();

            var (rating, color) = RateHf(result.vHF);
            var values = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), Margin = new Thickness(16, 16, 8, 16) };
            int row = 0;
            void Add(string label, string value, IBrush background = null)
            {
                values.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var l = new TextBlock { Text = label, Margin = new Thickness(0, 2, 12, 2) };
                var valueText = new TextBlock { Text = value };
                if (background != null)
                {
                    /* readable on the rating colors in light and dark theme */
                    valueText.Foreground = Brushes.Black;
                }
                var v = new Border { Background = background, Padding = new Thickness(4, 0), Child = valueText };
                Grid.SetRow(l, row); Grid.SetRow(v, row); Grid.SetColumn(v, 1);
                values.Children.Add(l); values.Children.Add(v);
                row++;
            }
            string V(double v) => v.ToString("0.00", CultureInfo.InvariantCulture) + " V";

            Add("HF:", "");
            Add("Voltage at 13.56 MHz:", V(result.vHF) + " (" + rating + ")", color);
            Add("", "");
            Add("LF:", "");
            Add("Voltage at 125 kHz:", V(result.vLF125));
            Add("Voltage at 134 kHz:", V(result.vLF134));
            Add("Optimal LF Frequency:", (result.GetPeakFrequency() / 1000.0f).ToString("0.00", CultureInfo.InvariantCulture) + " kHz");
            Add("Antenna voltage:", V(result.peakV));

            var note = new TextBlock { Text = "TeddyBench uses the HF (13.56 MHz) antenna only.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 0, 8, 16), MaxWidth = 300, Classes = { "hint" } };
            var left = new StackPanel();
            left.Children.Add(values);
            left.Children.Add(note);
            DockPanel.SetDock(left, Dock.Left);
            var panel = new DockPanel();
            panel.Children.Add(left);
            panel.Children.Add(plot);
            Content = panel;
        }

        /// <summary>Same thresholds as the WinForms version.</summary>
        public static (string rating, IBrush color) RateHf(double vHF)
        {
            if (vHF > 45) return ("critical", Brushes.Red);
            if (vHF > 40) return ("danger", Brushes.Yellow);
            if (vHF > 33) return ("wow", Brushes.Green);
            if (vHF > 25) return ("good", Brushes.GreenYellow);
            if (vHF > 20) return ("weak", Brushes.Yellow);
            if (vHF > 10) return ("meh", Brushes.Orange);
            return ("bad", Brushes.Red);
        }
    }

    /// <summary>Debug log (Options / Debug window). Hides instead of closing.</summary>
    public class LogWindow : Window
    {
        private readonly TextBox Box;
        private DateTime LastShown = DateTime.MinValue;
        private readonly DispatcherTimer Timer;

        public bool AllowClose { get; set; }

        public LogWindow()
        {
            Title = "TeddyBench Log";
            Width = 760;
            Height = 420;
            Box = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,Liberation Mono,monospace"),
                FontSize = 12
            };
            Content = Box;

            Timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (s, e) => Refresh());
            Timer.Start();
            Closing += (s, e) =>
            {
                if (!AllowClose)
                {
                    e.Cancel = true;
                    Hide();
                }
                else
                {
                    Timer.Stop();
                }
            };
        }

        private void Refresh()
        {
            DateTime last = Logger.LastUpdate;
            if (!IsVisible || last <= LastShown)
            {
                return;
            }
            Box.Text = Logger.GetText();
            Box.CaretIndex = Box.Text.Length;
            LastShown = last;
        }
    }
}
