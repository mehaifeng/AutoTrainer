using AutoTrainer.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Helpers
{
    public class CropImageHelper
    {
        public static void CropImage(string inputPath, string outputFolderPath, CropConfigModel CropConfig)
        {
            var CropPoint = new List<(string, Point, Point)>();
            foreach (var crop in CropConfig.Coprs)
            {
                (string, Point, Point) dualPoint = new()
                {
                    Item1 = crop.Name,
                    Item2 = new Point(crop.X1, crop.Y1),
                    Item3 = new Point(crop.X2, crop.Y2)
                };
                CropPoint.Add(dualPoint);
            }

            // 加载原始图像
            using var fileStream = File.OpenRead(inputPath);
            using var originalBitmap = new Bitmap(fileStream);

            int index = 0;
            foreach (var (type, topLeft, bottomRight) in CropPoint)
            {
                // 确保输出目录存在
                string path = Path.Combine(outputFolderPath, type);
                Directory.CreateDirectory(path);
                // 计算裁剪区域的宽高
                int cropWidth = (int)(bottomRight.X - topLeft.X);
                int cropHeight = (int)(bottomRight.Y - topLeft.Y);

                // 创建新的裁剪后的位图
                using var croppedBitmap = new RenderTargetBitmap(new PixelSize(cropWidth, cropHeight));

                // 创建绘图上下文
                using var context = croppedBitmap.CreateDrawingContext();

                // 创建裁剪区域
                var cropRect = new Rect(0, 0, cropWidth, cropHeight);

                // 绘制裁剪后的图像
                context.DrawImage(
                    originalBitmap,
                    new Rect(topLeft.X, topLeft.Y, cropWidth, cropHeight), // 源矩形
                    cropRect // 目标矩形
                );

                // 保存裁剪后的图像
                string inputName = Path.GetFileNameWithoutExtension(inputPath);
                string outputPath = Path.Combine(path, $"{inputName}_Crop{index}.png");
                croppedBitmap.Save(outputPath); // Avalonia默认保存为PNG格式

                Console.WriteLine($"Cropped image saved to: {outputPath}");
                index++;
            }
        }
    }
}
