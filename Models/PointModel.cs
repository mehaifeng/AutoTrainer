using Avalonia.Controls;
using Newtonsoft.Json;
using System;

namespace AutoTrainer.Models
{
    public class PointModel:AnnotationItem
    {
        public double X { get; set; }
        public double Y { get; set; }

        public PointModel() 
        {

        }

        public PointModel(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override BoundingBoxModel GetBoundingBox()
        {
            return new BoundingBoxModel
            {
                MinX = X,
                MinY = Y,
                MaxX = 0,
                MaxY = 0
            };
        }

        public override bool Contains(double x, double y)
        {
            return (x - X) * (x - X) + (y - Y) * (y - Y) <= 5 * 5;
        }

        public override AnnotationItem Clone()
        {
            return new PointModel(X, Y)
            {
                ClassName = this.ClassName,
                IsSelected = this.IsSelected,
                IsVisible = this.IsVisible,
            };
        }
    }
}
