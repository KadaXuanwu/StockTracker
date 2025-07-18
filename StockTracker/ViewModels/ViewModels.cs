using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using StockTracker.Models;
using StockTracker.Services;

namespace StockTracker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly StockDataService _stockDataService;
        private readonly IndicatorCalculationService _indicatorService;

        public ObservableCollection<StockViewModel> Stocks { get; } = new();
        public ObservableCollection<TimeRange> TimeRanges { get; } = new();

        [ObservableProperty]
        private StockViewModel? _selectedStock;

        [ObservableProperty]
        private TimeRange? _selectedTimeRange;

        [ObservableProperty]
        private string _newStockSymbol = string.Empty;

        [ObservableProperty]
        private int _windowPercentage = 40;

        [ObservableProperty]
        private double _linearRegressionWeight = 0.33;

        [ObservableProperty]
        private double _wmaWeight = 0.33;

        [ObservableProperty]
        private double _emaWeight = 0.34;

        [ObservableProperty]
        private double _weightSum = 1.0;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private Visibility _noSelectionVisibility = Visibility.Visible;

        [ObservableProperty]
        private Visibility _detailedViewVisibility = Visibility.Collapsed;

        public IndicatorSettings IndicatorSettings => new()
        {
            WindowPercentage = WindowPercentage,
            LinearRegressionWeight = LinearRegressionWeight,
            WMAWeight = WmaWeight,
            EMAWeight = EmaWeight
        };

        public MainViewModel()
        {
            _stockDataService = new StockDataService();
            _indicatorService = new IndicatorCalculationService();

            InitializeTimeRanges();
            _ = LoadSavedStocksAsync();
        }

        private void InitializeTimeRanges()
        {
            TimeRanges.Add(new TimeRange { Name = "1 Week", Days = 7 });
            TimeRanges.Add(new TimeRange { Name = "1 Month", Days = 30 });
            TimeRanges.Add(new TimeRange { Name = "3 Months", Days = 90 });
            TimeRanges.Add(new TimeRange { Name = "6 Months", Days = 180 });
            TimeRanges.Add(new TimeRange { Name = "1 Year", Days = 365 });
            TimeRanges.Add(new TimeRange { Name = "2 Years", Days = 730 });
            TimeRanges.Add(new TimeRange { Name = "5 Years", Days = 1825 });

            SelectedTimeRange = TimeRanges[1]; // Default to 1 Month
        }

        private async Task LoadSavedStocksAsync()
        {
            var symbols = await _stockDataService.LoadSavedStocksAsync();
            foreach (var symbol in symbols)
            {
                await LoadStockDataAsync(symbol);
            }
        }

        private async Task LoadStockDataAsync(string symbol)
        {
            if (SelectedTimeRange == null) return;

            var data = await _stockDataService.GetStockDataAsync(symbol, SelectedTimeRange.Days);
            if (data != null)
            {
                var stockVm = new StockViewModel(data, _indicatorService);
                stockVm.UpdateIndicators(IndicatorSettings);
                Stocks.Add(stockVm);
            }
        }

        [RelayCommand]
        private async Task AddStock()
        {
            if (string.IsNullOrWhiteSpace(NewStockSymbol))
            {
                StatusMessage = "Please enter a stock symbol";
                return;
            }

            var symbol = NewStockSymbol.ToUpper().Trim();

            if (Stocks.Any(s => s.Symbol == symbol))
            {
                StatusMessage = $"{symbol} is already being tracked";
                return;
            }

            IsLoading = true;
            StatusMessage = $"Adding {symbol}...";

            var isValid = await _stockDataService.ValidateStockSymbolAsync(symbol);
            if (!isValid)
            {
                StatusMessage = $"Invalid symbol: {symbol}";
                IsLoading = false;
                return;
            }

            await LoadStockDataAsync(symbol);

            // Save updated list
            var symbols = Stocks.Select(s => s.Symbol).ToList();
            await _stockDataService.SaveStocksAsync(symbols);

            NewStockSymbol = string.Empty;
            StatusMessage = $"{symbol} added successfully";
            IsLoading = false;
        }

        [RelayCommand]
        private async Task RemoveSelectedStock()
        {
            if (SelectedStock == null) return;

            var symbol = SelectedStock.Symbol;
            Stocks.Remove(SelectedStock);
            SelectedStock = null;

            var symbols = Stocks.Select(s => s.Symbol).ToList();
            await _stockDataService.SaveStocksAsync(symbols);

            StatusMessage = $"{symbol} removed";
        }

        [RelayCommand]
        private async Task RefreshData()
        {
            if (SelectedTimeRange == null) return;

            IsLoading = true;
            StatusMessage = "Refreshing data...";

            var tasks = Stocks.Select(async stock =>
            {
                var data = await _stockDataService.GetStockDataAsync(stock.Symbol, SelectedTimeRange.Days);
                if (data != null)
                {
                    stock.UpdateData(data);
                    stock.UpdateIndicators(IndicatorSettings);
                }
            });

            await Task.WhenAll(tasks);

            StatusMessage = "Data refreshed";
            IsLoading = false;
        }

        [RelayCommand]
        private void ApplyChanges()
        {
            WeightSum = LinearRegressionWeight + WmaWeight + EmaWeight;

            foreach (var stock in Stocks)
            {
                stock.UpdateIndicators(IndicatorSettings);
            }

            StatusMessage = "Changes applied";
        }

        partial void OnSelectedTimeRangeChanged(TimeRange? value)
        {
            if (value != null)
            {
                _ = RefreshData();
            }
        }

        partial void OnSelectedStockChanged(StockViewModel? value)
        {
            NoSelectionVisibility = value == null ? Visibility.Visible : Visibility.Collapsed;
            DetailedViewVisibility = value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        partial void OnLinearRegressionWeightChanged(double value)
        {
            WeightSum = LinearRegressionWeight + WmaWeight + EmaWeight;
        }

        partial void OnWmaWeightChanged(double value)
        {
            WeightSum = LinearRegressionWeight + WmaWeight + EmaWeight;
        }

        partial void OnEmaWeightChanged(double value)
        {
            WeightSum = LinearRegressionWeight + WmaWeight + EmaWeight;
        }
    }

    public partial class StockViewModel : ObservableObject
    {
        private readonly IndicatorCalculationService _indicatorService;
        private StockData _stockData;

        public string Symbol => _stockData.Symbol;
        public ObservableCollection<StockDataPoint> DataPoints { get; }

        [ObservableProperty]
        private decimal _currentPrice;

        [ObservableProperty]
        private decimal _priceChange;

        [ObservableProperty]
        private decimal _priceChangePercent;

        [ObservableProperty]
        private decimal _high;

        [ObservableProperty]
        private decimal _low;

        [ObservableProperty]
        private double[] _combinedIndicator = Array.Empty<double>();

        [ObservableProperty]
        private double[] _linearRegression = Array.Empty<double>();

        [ObservableProperty]
        private double[] _wma = Array.Empty<double>();

        [ObservableProperty]
        private double[] _ema = Array.Empty<double>();

        public StockViewModel(StockData stockData, IndicatorCalculationService indicatorService)
        {
            _stockData = stockData;
            _indicatorService = indicatorService;
            DataPoints = new ObservableCollection<StockDataPoint>(stockData.DataPoints);
            UpdateProperties();
        }

        public void UpdateData(StockData stockData)
        {
            _stockData = stockData;
            DataPoints.Clear();
            foreach (var point in stockData.DataPoints)
            {
                DataPoints.Add(point);
            }
            UpdateProperties();
        }

        private void UpdateProperties()
        {
            CurrentPrice = _stockData.CurrentPrice;
            PriceChange = _stockData.PriceChange;
            PriceChangePercent = _stockData.PriceChangePercent;
            High = _stockData.High;
            Low = _stockData.Low;
        }

        public void UpdateIndicators(IndicatorSettings settings)
        {
            var prices = DataPoints.Select(d => d.Close).ToArray();
            var window = Math.Max(2, (int)(prices.Length * settings.WindowPercentage / 100.0));

            LinearRegression = _indicatorService.CalculateLinearRegression(prices, window);
            Wma = _indicatorService.CalculateWMA(prices, window);
            Ema = _indicatorService.CalculateEMA(prices, window);
            CombinedIndicator = _indicatorService.CalculateCombinedIndicator(prices, settings);
        }
    }
}