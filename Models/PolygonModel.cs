using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace AutoTrainer.Models
{
    public class PolygonModel:AnnotationItem
    {
        public List<Point> Points { get; set; } = new List<Point>();

        public override BoundingBoxModel GetBoundingBox()
        {
            if (Points == null || Points.Count == 0)
                return new BoundingBoxModel();

            double minX = Points.Min(p => p.X);
            double minY = Points.Min(p => p.Y);
            double maxX = Points.Max(p => p.X);
            double maxY = Points.Max(p => p.Y);

            return new BoundingBoxModel
            {
                MinX = minX,
                MinY = minY,
                MaxX = maxX - minX,
                MaxY = maxY - minY,
            };
        }

        // 简单实现：使用射线法判断点是否在多边形内（适用于闭合多边形）
        public override bool Contains(double x, double y)
        {
            if (Points == null || Points.Count < 3) return false;

            bool inside = false;
            int n = Points.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((Points[i].Y > y) != (Points[j].Y > y)) &&
                    (x < (Points[j].X - Points[i].X) * (y - Points[i].Y) / (Points[j].Y - Points[i].Y) + Points[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public override AnnotationItem Clone()
        {
            var clone = new PolygonModel
            {
                ClassName = this.ClassName,
                IsSelected = this.IsSelected,
                IsVisible = this.IsVisible,
            };
            clone.Points.AddRange(this.Points.Select(p => new Point(p.X, p.Y)));
            return clone;
        }
    }
}
