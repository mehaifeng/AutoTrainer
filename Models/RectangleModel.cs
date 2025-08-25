using Avalonia.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Models
{
    public class RectangleModel:AnnotationItem
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public RectangleModel() { }

        public RectangleModel(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public override (double MinX, double MinY, double MaxX, double MaxY) GetBoundingBox()
        {
            return (X, Y, X + Width, Y + Height);
        }

        public override bool Contains(double x, double y)
        {
            return x >= X && x <= X + Width && y >= Y && y <= Y + Height;
        }

        public override AnnotationItem Clone()
        {
            return new RectangleModel(X, Y, Width, Height)
            {
                ClassName = this.ClassName,
                IsSelected = this.IsSelected,
                IsVisible = this.IsVisible,
                AnnotationType = this.AnnotationType
            };
        }
    }
}
