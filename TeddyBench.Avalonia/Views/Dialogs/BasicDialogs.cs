using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Threading;
using TeddyBench.Avalonia.Services;
using TeddyBench.Core;

namespace TeddyBench.Avalonia.Views.Dialogs
{
    public enum MessageButtons { Ok, YesNo, YesNoCancel }

    /// <summary>Message box. Result: "ok", "yes", "no" or null (cancel/closed).</summary>
    public class MessageDialog : DialogBase
    {
        public string Result { get; private set; }

        public MessageDialog(string text, string title, MessageButtons buttons, bool error = false) : base(title)
        {
            var body = new DockPanel { Margin = new Thickness(20), MaxWidth = 640 };
            Button[] row;
            switch (buttons)
            {
                case MessageButtons.YesNo:
                    row = new[] { MakeButton("Yes", () => Finish("yes"), isDefault: true), MakeButton("No", () => Finish("no"), isCancel: true) };
                    break;
                case MessageButtons.YesNoCancel:
                    row = new[] { MakeButton("Yes", () => Finish("yes"), isDefault: true), MakeButton("No", () => Finish("no")), MakeButton("Cancel", () => Finish(null), isCancel: true) };
                    break;
                default:
                    row = new[] { MakeButton("OK", () => Finish("ok"), isDefault: true, isCancel: true) };
                    break;
            }

            var buttonRow = ButtonRow(row);
            DockPanel.SetDock(buttonRow, Dock.Bottom);
            body.Children.Add(buttonRow);

            var message = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 600 };
            if (error)
            {
                var icon = new TextBlock { Text = "⚠", FontSize = 28, Foreground = Brushes.OrangeRed, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top };
                DockPanel.SetDock(icon, Dock.Left);
                body.Children.Add(icon);
            }
            body.Children.Add(message);
            Content = body;
        }

        private void Finish(string result)
        {
            Result = result;
            Close();
        }
    }

    /// <summary>One validated line of text (UID, hex ID, name).</summary>
    public class InputDialog : DialogBase
    {
        private readonly Func<string, string> Validate;
        protected readonly TextBox Input;
        private readonly TextBlock Hint;
        private readonly Button OkButton;

        /// <summary>The entered text, or null if cancelled.</summary>
        public string Result { get; private set; }

        /// <param name="validate">text -> error message, or null if valid.</param>
        public InputDialog(string title, string prompt, string initial, Func<string, string> validate = null, string watermark = null) : base(title)
        {
            Validate = validate ?? (s => null);
            Input = new TextBox { Text = initial ?? "", Watermark = watermark, MinWidth = 340, FontFamily = new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,Liberation Mono,monospace") };
            Hint = new TextBlock { FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
            OkButton = MakeButton("OK", Accept, isDefault: true);

            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
            panel.Children.Add(Text(prompt));
            panel.Children.Add(Input);
            panel.Children.Add(Hint);
            panel.Children.Add(ButtonRow(OkButton, MakeButton("Cancel", Close, isCancel: true)));
            Content = panel;

            Input.TextChanged += (s, e) => Revalidate();
            Opened += (s, e) =>
            {
                Input.Focus();
                Input.SelectAll();
            };
            Revalidate();
        }

        protected void Revalidate()
        {
            string error = Validate(Input.Text ?? "");
            OkButton.IsEnabled = error == null;
            Hint.Text = error ?? "✓ OK";
            Hint.Classes.Set("valid", error == null);
            Hint.Classes.Set("invalid", error != null);
        }

        private void Accept()
        {
            if (Validate(Input.Text ?? "") != null)
            {
                return;
            }
            Result = Input.Text;
            Close();
        }

        protected static bool IsHex(string s) => s.All(c => Uri.IsHexDigit(c));
    }

    /// <summary>Tag UID ("E00403" + 10 hex digits). Filled in automatically when a reader detects a tag.</summary>
    public class UidDialog : InputDialog
    {
        private readonly RfidReaderBase Reader;

        public UidDialog(RfidReaderBase reader, string initialUid)
            : base("Enter tag UID",
                  "You are about to add a file to the box." + Environment.NewLine + "Please enter the UID of the tag you want to assign it to." +
                  (reader != null ? Environment.NewLine + "Place the tag on the NFC reader to fill it in automatically." : ""),
                  string.IsNullOrEmpty(initialUid) ? "E00403" : initialUid, ValidateUid, "E00403xxxxxxxxxx")
        {
            Reader = reader;
            if (Reader != null)
            {
                Reader.UidFound += Reader_UidFound;
                Closed += (s, e) => Reader.UidFound -= Reader_UidFound;
            }
            Opened += (s, e) =>
            {
                /* select the part after the fixed prefix */
                Input.SelectionStart = Math.Min(6, Input.Text?.Length ?? 0);
                Input.SelectionEnd = Input.Text?.Length ?? 0;
            };
        }

        public static string ValidateUid(string uid)
        {
            if (!uid.StartsWith("E00403", StringComparison.OrdinalIgnoreCase))
            {
                return "Tonie tag UIDs start with E00403";
            }
            if (uid.Length != 16 || !IsHex(uid))
            {
                return "The UID must have 16 hex digits (" + uid.Length + "/16)";
            }
            return null;
        }

        private void Reader_UidFound(object sender, string uid)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (uid != null && Input.Text != uid)
                {
                    Input.Text = uid;
                    Input.Focus();
                }
            });
        }
    }

    /// <summary>Report to RevvoX: name, comment and the (read-only) content that will be sent.</summary>
    public class ReportDialog : DialogBase
    {
        public static string DefaultMessage = "<please provide information for every file>";

        public ReportResult Result { get; private set; }

        public ReportDialog(string content, string defaultUsername) : base("Report to RevvoX")
        {
            CanResize = true;
            SizeToContent = SizeToContent.Manual;
            Width = 640;
            Height = 520;

            var user = new TextBox { Text = defaultUsername ?? "" };
            var message = new TextBox { Text = DefaultMessage, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80 };
            var contentBox = new TextBox
            {
                Text = content,
                IsReadOnly = true,
                AcceptsReturn = true,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,Liberation Mono,monospace"),
                FontSize = 12
            };

            var grid = new Grid { Margin = new Thickness(16), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*,Auto"), RowSpacing = 4 };
            void Add(Control c, int row) { Grid.SetRow(c, row); grid.Children.Add(c); }
            Add(new TextBlock { Text = "Your name:" }, 0);
            Add(user, 1);
            Add(new TextBlock { Text = "Comment:", Margin = new Thickness(0, 8, 0, 0) }, 2);
            Add(message, 3);
            Add(new TextBlock { Text = "Content:", Margin = new Thickness(0, 8, 0, 0) }, 4);
            Add(contentBox, 5);
            Add(ButtonRow(MakeButton("Send", () =>
            {
                Result = new ReportResult { Username = user.Text ?? "", Message = message.Text ?? "" };
                DefaultMessage = Result.Message;
                Close();
            }, isDefault: false), MakeButton("Cancel", Close, isCancel: true)), 6);
            Content = grid;
        }
    }

    /// <summary>Shows a tag dump (read-only) or asks for one (hex).</summary>
    public class TagDumpDialog : DialogBase
    {
        public string Result { get; private set; }

        public TagDumpDialog(bool display, string initial) : base("Tag Dump")
        {
            var box = new TextBox
            {
                Text = initial ?? "",
                IsReadOnly = display,
                AcceptsReturn = false,
                TextWrapping = TextWrapping.Wrap,
                Width = 520,
                Height = 90,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,Liberation Mono,monospace")
            };

            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
            panel.Children.Add(Text(display ? "This is the UID and memory content of your tag" : "Enter the UID and memory content of your tag"));
            panel.Children.Add(box);

            var ok = MakeButton("OK", () => { Result = (box.Text ?? "").Trim(); Close(); }, isDefault: true);
            panel.Children.Add(display ? ButtonRow(ok) : ButtonRow(ok, MakeButton("Cancel", Close, isCancel: true)));
            Content = panel;
        }
    }

    /// <summary>"Do you want to download the new version?"</summary>
    public class UpdateDialog : MessageDialog
    {
        public UpdateDialog(string version, string releaseName, bool canInstall)
            : base((canInstall ? "Do you want to download the new version?" : "A new version is available. Do you want to open the release page?") +
                   Environment.NewLine + "Title: " + releaseName, "Update: " + version, MessageButtons.YesNo)
        {
        }
    }

    /// <summary>Modeless progress window for reader operations.</summary>
    public class OperationDialog : DialogBase, IOperationHandle
    {
        private readonly CancellationTokenSource Cts = new CancellationTokenSource();
        private bool ClosedByOwner;

        public CancellationToken Cancelled => Cts.Token;

        public OperationDialog(string title, bool cancellable) : base(title)
        {
            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12, Width = 320 };
            panel.Children.Add(Text("Please hold the tag on the reader..."));
            panel.Children.Add(new ProgressBar { IsIndeterminate = true });
            var cancel = MakeButton("Cancel", OnCancel, isCancel: true);
            cancel.IsEnabled = cancellable;
            panel.Children.Add(ButtonRow(cancel));
            Content = panel;

            Closed += (s, e) => Cts.Cancel();
        }

        protected override void OnCancel()
        {
            Cts.Cancel();
            Close();
        }

        void IOperationHandle.Close()
        {
            if (!ClosedByOwner)
            {
                ClosedByOwner = true;
                Close();
            }
        }
    }
}
