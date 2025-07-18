using System;
using System.Collections.Generic;

namespace StockTracker.Models
{
    public class StockData
    {
        public string Symbol { get; set; } = string.Empty;
        public List<StockDataPoint> DataPoints { get; set; } = new List<StockDataPoint>();
        public decimal CurrentPrice { get; set; }
        public decimal PriceChange { get; set; }
        public decimal PriceChangePercent { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
    }

    public class StockDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Close { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public long Volume { get; set; }
    }

    public class IndicatorSettings
    {
        public int WindowPercentage { get; set; } = 40;
        public double LinearRegressionWeight { get; set; } = 0.33;
        public double WMAWeight { get; set; } = 0.33;
        public double EMAWeight { get; set; } = 0.34;
    }

    public class TimeRange
    {
        public string Name { get; set; } = string.Empty;
        public int Days { get; set; }
    }

    public class SavedStockList
    {
        public List<string> Symbols { get; set; } = new List<string>();
    }
}