using Avalonia;

namespace AutoTrainer.Models
{
    /// <summary>
    /// 调整手柄类型
    /// </summary>
    public enum HandleType
    {
        TopLeft,
        Top,
        TopRight,
        Left,
        Right,
        BottomLeft,
        Bottom,
        BottomRight
    }

    /// <summary>
    /// 调整手柄模型
    /// </summary>
    public class ResizeHandle
    {
        /// <summary>
        /// 手柄类型
        /// </summary>
        public HandleType Type { get; set; }

        /// <summary>
        /// 手柄边界
        /// </summary>
        public Rect Bounds { get; set; }

        public ResizeHandle(HandleType type, Rect bounds)
        {
            Type = type;
            Bounds = bounds;
        }
    }
}
