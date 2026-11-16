using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System;
using System.Linq;
using TeddyBench.Avalonia.ViewModels;

namespace TeddyBench.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel Subscribed;

        public MainWindow()
        {
            InitializeComponent();

            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);
            TonieList.DoubleTapped += (s, e) => ViewModel?.ReassignSelectedCommand.Execute(null);
            EncodeLogBox.PropertyChanged += (s, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                {
                    EncodeLogBox.CaretIndex = EncodeLogBox.Text?.Length ?? 0;
                }
            };
        }

        private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (Subscribed != null)
            {
                Subscribed.ScrollIntoViewRequested -= ScrollIntoView;
            }
            Subscribed = ViewModel;
            if (Subscribed != null)
            {
                Subscribed.ScrollIntoViewRequested += ScrollIntoView;
            }
        }

        private void ScrollIntoView(TonieItemViewModel item)
        {
            TonieList.ScrollIntoView(item);
            TonieList.Focus();
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).Where(p => p != null).ToArray();
            if (files != null && files.Length > 0 && ViewModel != null)
            {
                await ViewModel.DropFiles(files);
            }
        }
    }
}
