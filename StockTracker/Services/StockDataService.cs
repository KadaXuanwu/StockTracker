using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using StockTracker.Models;
using MathNet.Numerics;

namespace StockTracker.Services
{
    public class StockDataService
    {
        private readonly HttpClient _httpClient;
        private readonly string _stocksFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StockTracker",
            "stocks.json"
        );

        public StockDataService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var directory = Path.GetDirectoryName(_stocksFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public async Task<StockData?> GetStockDataAsync(string symbol, int days)
        {
            try
            {
                // Calculate Unix timestamps
                var endDate = DateTimeOffset.UtcNow;
                var startDate = endDate.AddDays(-days);
                var period1 = startDate.ToUnixTimeSeconds();
                var period2 = endDate.ToUnixTimeSeconds();

                // Use Yahoo Finance API v8
                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?period1={period1}&period2={period2}&interval=1d";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonDocument.Parse(json);

                var chart = data.RootElement.GetProperty("chart").GetProperty("result")[0];
                var timestamps = chart.GetProperty("timestamp").EnumerateArray().Select(x => x.GetInt64()).ToArray();
                var quotes = chart.GetProperty("indicators").GetProperty("quote")[0];

                var closes = quotes.GetProperty("close").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                var opens = quotes.GetProperty("open").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                var highs = quotes.GetProperty("high").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                var lows = quotes.GetProperty("low").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                var volumes = quotes.GetProperty("volume").EnumerateArray().Select(x => x.GetInt64()).ToArray();

                var dataPoints = new List<StockDataPoint>();
                for (int i = 0; i < timestamps.Length; i++)
                {
                    if (!double.IsNaN(closes[i]))
                    {
                        dataPoints.Add(new StockDataPoint
                        {
                            Date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).DateTime,
                            Close = (decimal)closes[i],
                            Open = (decimal)opens[i],
                            High = (decimal)highs[i],
                            Low = (decimal)lows[i],
                            Volume = volumes[i]
                        });
                    }
                }

                if (!dataPoints.Any()) return null;

                var prices = dataPoints.Select(d => d.Close).ToArray();
                var currentPrice = prices.Last();
                var firstPrice = prices.First();

                return new StockData
                {
                    Symbol = symbol,
                    DataPoints = dataPoints,
                    CurrentPrice = currentPrice,
                    PriceChange = currentPrice - firstPrice,
                    PriceChangePercent = ((currentPrice - firstPrice) / firstPrice) * 100,
                    High = prices.Max(),
                    Low = prices.Min()
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching data for {symbol}: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ValidateStockSymbolAsync(string symbol)
        {
            try
            {
                var data = await GetStockDataAsync(symbol, 5);
                return data != null && data.DataPoints.Any();
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> LoadSavedStocksAsync()
        {
            try
            {
                if (File.Exists(_stocksFilePath))
                {
                    var json = await File.ReadAllTextAsync(_stocksFilePath);
                    var savedList = JsonSerializer.Deserialize<SavedStockList>(json);
                    return savedList?.Symbols ?? new List<string>();
                }
            }
            catch { }
            return new List<string>();
        }

        public async Task SaveStocksAsync(List<string> symbols)
        {
            try
            {
                var savedList = new SavedStockList { Symbols = symbols };
                var json = JsonSerializer.Serialize(savedList, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_stocksFilePath, json);
            }
            catch { }
        }
    }

    public class IndicatorCalculationService
    {
        public double[] CalculateLinearRegression(decimal[] prices, int window)
        {
            var doublePrices = prices.Select(p => (double)p).ToArray();
            var result = new double[prices.Length];
            Array.Fill(result, double.NaN);

            for (int i = window - 1; i < prices.Length; i++)
            {
                var x = Enumerable.Range(0, window).Select(j => (double)j).ToArray();
                var y = doublePrices.Skip(i - window + 1).Take(window).ToArray();

                var regression = Fit.Line(x, y);
                result[i] = regression.Item1 + regression.Item2 * (window - 1);
            }

            return result;
        }

        public double[] CalculateWMA(decimal[] prices, int window)
        {
            var result = new double[prices.Length];
            Array.Fill(result, double.NaN);

            for (int i = window - 1; i < prices.Length; i++)
            {
                double sum = 0;
                double weightSum = 0;

                for (int j = 0; j < window; j++)
                {
                    var weight = j + 1;
                    sum += (double)prices[i - window + 1 + j] * weight;
                    weightSum += weight;
                }

                result[i] = sum / weightSum;
            }

            return result;
        }

        public double[] CalculateEMA(decimal[] prices, int window)
        {
            var result = new double[prices.Length];
            Array.Fill(result, double.NaN);

            var alpha = 2.0 / (window + 1);

            // Start with SMA for first value
            double sum = 0;
            for (int i = 0; i < window && i < prices.Length; i++)
            {
                sum += (double)prices[i];
            }
            result[window - 1] = sum / window;

            // Calculate EMA for remaining values
            for (int i = window; i < prices.Length; i++)
            {
                result[i] = alpha * (double)prices[i] + (1 - alpha) * result[i - 1];
            }

            return result;
        }

        public double[] CalculateCombinedIndicator(decimal[] prices, IndicatorSettings settings)
        {
            var window = Math.Max(2, (int)(prices.Length * settings.WindowPercentage / 100.0));

            var lr = CalculateLinearRegression(prices, window);
            var wma = CalculateWMA(prices, window);
            var ema = CalculateEMA(prices, window);

            var result = new double[prices.Length];
            var totalWeight = settings.LinearRegressionWeight + settings.WMAWeight + settings.EMAWeight;

            if (totalWeight == 0)
            {
                Array.Fill(result, double.NaN);
                return result;
            }

            var lrNorm = settings.LinearRegressionWeight / totalWeight;
            var wmaNorm = settings.WMAWeight / totalWeight;
            var emaNorm = settings.EMAWeight / totalWeight;

            for (int i = 0; i < prices.Length; i++)
            {
                if (!double.IsNaN(lr[i]) && !double.IsNaN(wma[i]) && !double.IsNaN(ema[i]))
                {
                    result[i] = lrNorm * lr[i] + wmaNorm * wma[i] + emaNorm * ema[i];
                }
                else
                {
                    result[i] = double.NaN;
                }
            }

            return result;
        }
    }
}