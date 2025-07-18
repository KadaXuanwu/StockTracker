using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using StockTracker.ViewModels;
using Windows.System;
using Microsoft.UI;
using System;
using Windows.UI;

namespace StockTracker
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Stock Tracker";

            // Set window size
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1400, Height = 800 });

            ViewModel = new MainViewModel();
        }

        private void OnSymbolTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && ViewModel.AddStockCommand.CanExecute(null))
            {
                ViewModel.AddStockCommand.Execute(null);
            }
        }

        private void OnStockItemPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is StockViewModel stock)
            {
                ViewModel.SelectedStock = stock;
            }
        }

        private void OnStockItemPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 74, 74, 74));
            }
        }

        private void OnStockItemPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = (Brush)Application.Current.Resources["BorderBrush"];
            }
        }

        private string FormatWeight(double weight)
        {
            return weight.ToString("0.00");
        }

        private string FormatPrice(decimal price)
        {
            return $"€{price:F2}";
        }

        private string FormatPriceChange(decimal change, decimal changePercent)
        {
            return $"€{change:+0.00;-0.00} ({changePercent:+0.0;-0.0}%)";
        }

        private Brush GetPriceChangeColor(decimal change)
        {
            return change >= 0 ?
                (Brush)Application.Current.Resources["GreenBrush"] :
                (Brush)Application.Current.Resources["RedBrush"];
        }

        private string GetCombinedIndicatorLabel()
        {
            return $"Combined ({ViewModel.LinearRegressionWeight:F2}LR + {ViewModel.WmaWeight:F2}WMA + {ViewModel.EmaWeight:F2}EMA)";
        }
    }
}