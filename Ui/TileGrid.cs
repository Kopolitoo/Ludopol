using System;
using System.Windows;
using System.Windows.Controls;

namespace Ludopol
{
    /// <summary>
    /// VIEW: lays tiles out in rows, like a WrapPanel, but stretches them so that each row
    /// fills the whole width: as many columns as fit at ItemMinWidth, then the space left is
    /// shared between them. Each row is as high as its highest tile.
    /// Used by the lists of games and by the categories, tags and developers pages.
    /// </summary>
    public sealed class TileGrid : Panel
    {
        /// <summary>Smallest width of a tile, margins included (the TileSlotWidth resource).</summary>
        public static readonly DependencyProperty ItemMinWidthProperty = DependencyProperty.Register(
            nameof(ItemMinWidth), typeof(double), typeof(TileGrid),
            new FrameworkPropertyMetadata(320.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double ItemMinWidth
        {
            get => (double)GetValue(ItemMinWidthProperty);
            set => SetValue(ItemMinWidthProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            (int columns, double width) = Columns(availableSize.Width);
            double total = 0;
            double rowHeight = 0;
            int count = 0;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                count++;
                if (count % columns == 0)
                {
                    total += rowHeight;
                    rowHeight = 0;
                }
            }
            total += rowHeight;
            double usedWidth = double.IsInfinity(availableSize.Width) ? columns * width : availableSize.Width;
            return new Size(usedWidth, total);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            (int columns, double width) = Columns(finalSize.Width);
            double y = 0;
            for (int start = 0; start < InternalChildren.Count; start += columns)
            {
                int end = Math.Min(start + columns, InternalChildren.Count);
                double rowHeight = 0;
                for (int i = start; i < end; i++)
                {
                    rowHeight = Math.Max(rowHeight, InternalChildren[i].DesiredSize.Height);
                }
                for (int i = start; i < end; i++)
                {
                    InternalChildren[i].Arrange(new Rect((i - start) * width, y, width, rowHeight));
                }
                y += rowHeight;
            }
            return finalSize;
        }

        /// <summary>How many columns fit in a width, and the width of each.</summary>
        private (int Columns, double Width) Columns(double available)
        {
            double minimum = Math.Max(1, ItemMinWidth);
            if (double.IsInfinity(available) || double.IsNaN(available))
            {
                return (Math.Max(1, InternalChildren.Count), minimum);
            }
            int columns = Math.Max(1, (int)Math.Floor(available / minimum));
            return (columns, Math.Floor(available / columns));
        }
    }
}
