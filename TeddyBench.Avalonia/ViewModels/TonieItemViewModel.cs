using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TeddyBench.Core;

namespace TeddyBench.Avalonia.ViewModels
{
    /// <summary>One tonie file in the main list.</summary>
    public partial class TonieItemViewModel : ObservableObject
    {
        public CardEntry Entry { get; }

        [ObservableProperty]
        private string text;

        [ObservableProperty]
        private string toolTip;

        /// <summary>"unknown", "custom" or the audio hash (see <see cref="TonieResolver"/>).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GroupName))]
        private string imageKey = TonieResolver.ImageUnknown;

        [ObservableProperty]
        private IImage image;

        public TonieItemViewModel(CardEntry entry, IImage placeholder)
        {
            Entry = entry;
            text = entry.InitialText;
            toolTip = entry.FileName;
            image = placeholder;
        }

        public bool IsUnknown => ImageKey == TonieResolver.ImageUnknown;
        public bool IsCustom => ImageKey == TonieResolver.ImageCustom;

        /// <summary>Group shown when sorting by type (same groups as the WinForms list).</summary>
        public string GroupName => IsUnknown ? "Unknown" : IsCustom ? "Custom" : "Retail";

        /// <summary>Order of the groups when sorting by type.</summary>
        public int GroupOrder => IsUnknown ? 0 : IsCustom ? 1 : 2;
    }
}
