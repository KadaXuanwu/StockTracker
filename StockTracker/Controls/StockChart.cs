using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using StockTracker.Models;
using Windows.Foundation;
using Windows.UI;

namespace StockTracker.Controls
{
    public class StockChart : UserControl
    {
        private CanvasControl? _canvas;
        private bool _isPointerOver;
        private Point _pointerPosition;
        private int _hoveredIndex = -1;

        public static readonly DependencyProperty DataPointsProperty =
            DependencyProperty.Register(nameof(DataPoints), typeof(IList<StockDataPoint>),
                typeof(StockChart), new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty CombinedIndicatorProperty =
            DependencyProperty.Register(nameof(CombinedIndicator), typeof(double[]),
                typeof(StockChart), new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty LinearRegressionProperty =
            DependencyProperty.Register(nameof(LinearRegression), typeof(double[]),
                typeof(StockChart), new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty WMAProperty =
            DependencyProperty.Register(nameof(WMA), typeof(double[]),
                typeof(StockChart), new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty EMAProperty =
            DependencyProperty.Register(nameof(EMA), typeof(double[]),
                typeof(StockChart), new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty ShowDetailedViewProperty =
            DependencyProperty.Register(nameof(ShowDetailedView), typeof(bool),
                typeof(StockChart), new PropertyMetadata(false, OnDataChanged));

        public static readonly DependencyProperty ChartHeightProperty =
            DependencyProperty.Register(nameof(ChartHeight), typeof(double),
                typeof(StockChart), new PropertyMetadata(150.0));

        public IList<StockDataPoint>? DataPoints
        {
            get => (IList<StockDataPoint>?)GetValue(DataPointsProperty);
            set => SetValue(DataPointsProperty, value);
        }

        public double[]? CombinedIndicator
        {
            get => (double[]?)GetValue(CombinedIndicatorProperty);
            set => SetValue(CombinedIndicatorProperty, value);
        }

        public double[]? LinearRegression
        {
            get => (double[]?)GetValue(LinearRegressionProperty);
            set => SetValue(LinearRegressionProperty, value);
        }

        public double[]? WMA
        {
            get => (double[]?)GetValue(WMAProperty);
            set => SetValue(WMAProperty, value);
        }

        public double[]? EMA
        {
            get => (double[]?)GetValue(EMAProperty);
            set => SetValue(EMAProperty, value);
        }

        public bool ShowDetailedView
        {
            get => (bool)GetValue(ShowDetailedViewProperty);
            set => SetValue(ShowDetailedViewProperty, value);
        }

        public double ChartHeight
        {
            get => (double)GetValue(ChartHeightProperty);
            set => SetValue(ChartHeightProperty, value);
        }

        public StockChart()
        {
            _canvas = new CanvasControl();
            _canvas.Draw += OnCanvasDraw;

            if (!ShowDetailedView)
            {
                _canvas.Height = ChartHeight;
            }

            Content = _canvas;

            PointerMoved += OnPointerMoved;
            PointerEntered += OnPointerEntered;
            PointerExited += OnPointerExited;
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StockChart chart)
            {
                chart._canvas?.Invalidate();
            }
        }

        private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var session = args.DrawingSession;
            var size = sender.Size;

            if (DataPoints == null || DataPoints.Count < 2) return;

            var margin = ShowDetailedView ? 60 : 20;
            var chartArea = new Rect(margin, margin, size.Width - margin * 2, size.Height - margin * 2);

            // Background
            session.FillRectangle(chartArea, ShowDetailedView ? Color.FromArgb(255, 26, 26, 26) : Colors.Transparent);

            // Calculate bounds
            var prices = DataPoints.Select(d => (float)d.Close).ToArray();
            var minPrice = prices.Min();
            var maxPrice = prices.Max();
            var priceRange = maxPrice - minPrice;
            var padding = priceRange * 0.1f;
            minPrice -= padding;
            maxPrice += padding;

            // Draw grid lines if detailed view
            if (ShowDetailedView)
            {
                DrawGridLines(session, chartArea, minPrice, maxPrice);
            }

            // Create points for the price line
            var points = new List<Vector2>();
            for (int i = 0; i < DataPoints.Count; i++)
            {
                var x = (float)(chartArea.X + (i / (double)(DataPoints.Count - 1)) * chartArea.Width);
                var y = (float)(chartArea.Y + chartArea.Height - ((prices[i] - minPrice) / (maxPrice - minPrice)) * chartArea.Height);
                points.Add(new Vector2(x, y));
            }

            // Determine color based on price change
            var priceColor = prices.Last() >= prices.First() ?
                Color.FromArgb(255, 0, 255, 0) : Color.FromArgb(255, 255, 68, 68);

            // Draw individual indicators if detailed view
            if (ShowDetailedView)
            {
                DrawIndicator(session, LinearRegression, chartArea, minPrice, maxPrice,
                    Color.FromArgb(128, 255, 102, 102), true); // Red with transparency
                DrawIndicator(session, WMA, chartArea, minPrice, maxPrice,
                    Color.FromArgb(128, 102, 255, 102), true); // Green with transparency
                DrawIndicator(session, EMA, chartArea, minPrice, maxPrice,
                    Color.FromArgb(128, 102, 102, 255), true); // Blue with transparency
            }

            // Draw combined indicator
            DrawIndicator(session, CombinedIndicator, chartArea, minPrice, maxPrice,
                Colors.White, false);

            // Draw price line
            if (points.Count > 1)
            {
                using (var pathBuilder = new CanvasPathBuilder(session))
                {
                    pathBuilder.BeginFigure(points[0]);
                    for (int i = 1; i < points.Count; i++)
                    {
                        pathBuilder.AddLine(points[i]);
                    }
                    pathBuilder.EndFigure(CanvasFigureLoop.Open);

                    using (var geometry = CanvasGeometry.CreatePath(pathBuilder))
                    {
                        session.DrawGeometry(geometry, priceColor, ShowDetailedView ? 2.5f : 1.5f);
                    }
                }
            }

            // Draw hover info if in detailed view
            if (ShowDetailedView && _isPointerOver && _hoveredIndex >= 0 && _hoveredIndex < DataPoints.Count)
            {
                DrawHoverInfo(session, chartArea, _hoveredIndex, minPrice, maxPrice);
            }

            // Draw price info
            if (!ShowDetailedView)
            {
                DrawPriceInfo(session, size, prices, priceColor);
            }
        }

        private void DrawIndicator(CanvasDrawingSession session, double[]? values, Rect chartArea,
            float minPrice, float maxPrice, Color color, bool isDashed)
        {
            if (values == null || values.Length == 0) return;

            var points = new List<Vector2>();
            for (int i = 0; i < values.Length; i++)
            {
                if (!double.IsNaN(values[i]))
                {
                    var x = (float)(chartArea.X + (i / (double)(values.Length - 1)) * chartArea.Width);
                    var y = (float)(chartArea.Y + chartArea.Height -
                        ((float)values[i] - minPrice) / (maxPrice - minPrice) * chartArea.Height);
                    points.Add(new Vector2(x, y));
                }
            }

            if (points.Count > 1)
            {
                using (var pathBuilder = new CanvasPathBuilder(session))
                {
                    pathBuilder.BeginFigure(points[0]);
                    for (int i = 1; i < points.Count; i++)
                    {
                        pathBuilder.AddLine(points[i]);
                    }
                    pathBuilder.EndFigure(CanvasFigureLoop.Open);

                    using (var geometry = CanvasGeometry.CreatePath(pathBuilder))
                    {
                        if (isDashed)
                        {
                            var strokeStyle = new CanvasStrokeStyle
                            {
                                DashStyle = CanvasDashStyle.Dash,
                                DashOffset = 0
                            };
                            session.DrawGeometry(geometry, color, 1.5f, strokeStyle);
                        }
                        else
                        {
                            session.DrawGeometry(geometry, color, ShowDetailedView ? 2f : 1f);
                        }
                    }
                }
            }
        }

        private void DrawGridLines(CanvasDrawingSession session, Rect chartArea, float minPrice, float maxPrice)
        {
            var gridColor = Color.FromArgb(51, 255, 255, 255); // 20% white

            // Horizontal grid lines
            for (int i = 0; i <= 5; i++)
            {
                var y = (float)(chartArea.Y + (i / 5.0) * chartArea.Height);
                session.DrawLine(
                    new Vector2((float)chartArea.X, y),
                    new Vector2((float)(chartArea.X + chartArea.Width), y),
                    gridColor, 0.5f);

                // Price labels
                var price = maxPrice - (i / 5.0f) * (maxPrice - minPrice);
                session.DrawText($"€{price:F2}",
                    new Vector2((float)(chartArea.X - 5), y - 8),
                    Color.FromArgb(255, 136, 136, 136),
                    new CanvasTextFormat
                    {
                        FontSize = 10,
                        HorizontalAlignment = CanvasHorizontalAlignment.Right
                    });
            }
        }

        private void DrawPriceInfo(CanvasDrawingSession session, Size size, float[] prices, Color priceColor)
        {
            var currentPrice = prices.Last();
            var priceChange = ((prices.Last() - prices.First()) / prices.First()) * 100;
            var text = $"€{currentPrice:F2} ({priceChange:+0.0;-0.0}%)";

            var textFormat = new CanvasTextFormat
            {
                FontSize = 9,
                HorizontalAlignment = CanvasHorizontalAlignment.Right,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };

            session.DrawText(text,
                new Vector2((float)(size.Width - 5), 5),
                priceColor,
                textFormat);
        }

        private void DrawHoverInfo(CanvasDrawingSession session, Rect chartArea, int index,
            float minPrice, float maxPrice)
        {
            var dataPoint = DataPoints![index];
            var price = (float)dataPoint.Close;

            var x = (float)(chartArea.X + (index / (double)(DataPoints.Count - 1)) * chartArea.Width);
            var y = (float)(chartArea.Y + chartArea.Height - ((price - minPrice) / (maxPrice - minPrice)) * chartArea.Height);

            // Draw vertical line
            session.DrawLine(
                new Vector2(x, (float)chartArea.Y),
                new Vector2(x, (float)(chartArea.Y + chartArea.Height)),
                Color.FromArgb(128, 255, 255, 255), 1f);

            // Draw hover box
            var text = $"{dataPoint.Date:yyyy-MM-dd}\n€{price:F2}";
            var textFormat = new CanvasTextFormat { FontSize = 12 };
            var textLayout = new CanvasTextLayout(session, text, textFormat, 200, 100);

            var boxX = x + 20;
            var boxY = y - 30;

            // Adjust position if near edge
            if (boxX + textLayout.DrawBounds.Width > chartArea.Right - 20)
            {
                boxX = x - (float)textLayout.DrawBounds.Width - 20;
            }

            session.FillRectangle(
                new Rect(boxX - 5, boxY - 5,
                    textLayout.DrawBounds.Width + 10,
                    textLayout.DrawBounds.Height + 10),
                Color.FromArgb(230, 58, 58, 58));

            session.DrawTextLayout(textLayout, new Vector2(boxX, boxY), Colors.White);
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!ShowDetailedView || DataPoints == null || DataPoints.Count == 0) return;

            _pointerPosition = e.GetCurrentPoint(this).Position;

            var margin = 60;
            var chartArea = new Rect(margin, margin, ActualWidth - margin * 2, ActualHeight - margin * 2);

            if (chartArea.Contains(_pointerPosition))
            {
                var relativeX = (_pointerPosition.X - chartArea.X) / chartArea.Width;
                _hoveredIndex = (int)(relativeX * (DataPoints.Count - 1));
                _hoveredIndex = Math.Max(0, Math.Min(DataPoints.Count - 1, _hoveredIndex));
            }
            else
            {
                _hoveredIndex = -1;
            }

            _canvas?.Invalidate();
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _isPointerOver = true;
            _canvas?.Invalidate();
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            _isPointerOver = false;
            _hoveredIndex = -1;
            _canvas?.Invalidate();
        }
    }
}