using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform;
using System;

namespace TeddyBench.Avalonia.Views.Dialogs
{
    /// <summary>Common look of the TeddyBench dialogs (built in code; they are small).</summary>
    public class DialogBase : Window
    {
        public DialogBase(string title)
        {
            Title = title;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;
            ShowInTaskbar = false;
            MinWidth = 320;
            try
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://TeddyBench/Assets/dog.ico")));
            }
            catch (Exception)
            {
            }

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    OnCancel();
                }
            };
        }

        /// <summary>Escape key / window close without a choice.</summary>
        protected virtual void OnCancel()
        {
            Close();
        }

        protected static Button MakeButton(string text, Action onClick, bool isDefault = false, bool isCancel = false)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 88,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                IsDefault = isDefault,
                IsCancel = isCancel
            };
            button.Click += (s, e) => onClick();
            return button;
        }

        protected static StackPanel ButtonRow(params Button[] buttons)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(0, 16, 0, 0)
            };
            foreach (var b in buttons)
            {
                row.Children.Add(b);
            }
            return row;
        }

        protected static TextBlock Text(string text, double maxWidth = 560)
        {
            return new TextBlock { Text = text, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, MaxWidth = maxWidth };
        }
    }
}
