using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Controls;
using R3P.Hivemind.Core.Diagnostics;

namespace R3P.Hivemind.UI.Wpf
{
    public partial class CrashConsoleView : UserControl
    {
        private readonly CrashConsoleViewModel _viewModel = new();

        public CrashConsoleView()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.Lines.CollectionChanged += OnLinesChanged;
            ScrollToEnd();
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.Lines.CollectionChanged -= OnLinesChanged;
            _viewModel.Dispose();
        }

        private void OnLinesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ScrollToEnd();
        }

        private void ScrollToEnd()
        {
            if (Scroller == null) return;
            Scroller.Dispatcher.InvokeAsync(() => Scroller.ScrollToEnd());
        }

        private sealed class CrashConsoleViewModel : IDisposable
        {
            public CrashConsoleViewModel()
            {
                Lines = new ObservableCollection<string>(CrashLogStore.GetSnapshot());
                CrashLogStore.LineAppended += OnLineAppended;
            }

            public ObservableCollection<string> Lines { get; }

            public void OnLineAppended(object sender, string message)
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    Lines.Add(message);
                });
            }

            public void Dispose()
            {
                CrashLogStore.LineAppended -= OnLineAppended;
            }
        }
    }
}

