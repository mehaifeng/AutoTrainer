using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace AutoTrainer.Helpers
{
    public class ImageAugmentation
    {

        public static List<string> AugmentImageOne(int index, string originalImagePath, string outputDirectory, int augmentationCount)
        {
            if (!File.Exists(originalImagePath))
            {
                throw new FileNotFoundException("原始图像文件不存在。", originalImagePath);
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            List<string> augmentedImagePaths = new List<string>();

            try
            {
                using (Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(originalImagePath))
                {
                    for (int i = 0; i < augmentationCount; i++)
                    {
                        // 克隆原始图像，避免修改原始图像
                        Image<Rgba32> augmentedImage = image.Clone();

                        // 应用随机增强效果
                        Random random = new Random();
                        int randomRotation = random.Next(-15, 15); // 随机旋转 -30 到 30 度
                        double randomZoom = 1.0 + (random.NextDouble() * 0.2 - 0.1); // 随机缩放 0.9 到 1.1 倍
                        double randomBrightness = 1.0 + (random.NextDouble() * 0.5 - 0.2); // 随机亮度调整 0.8 到 1.3 倍
                        double randomContrast = 1.0 + (random.NextDouble() * 0.5 - 0.2); // 随机对比度调整 0.7 到 1.3 倍
                        bool randomFlipHorizontal = random.Next(2) == 0; // 随机水平翻转
                        bool randomFlipVertical = random.Next(2) == 0;//随机垂直翻转

                        augmentedImage.Mutate(x => x
                            .Rotate(randomRotation)
                            .Resize((int)(image.Width * randomZoom), (int)(image.Height * randomZoom))
                            .Brightness((float)randomBrightness)
                            .Contrast((float)randomContrast));
                        if (randomFlipHorizontal)
                            augmentedImage.Mutate(x => x.Flip(FlipMode.Horizontal));
                        if (randomFlipVertical)
                            augmentedImage.Mutate(x => x.Flip(FlipMode.Vertical));

                        // 生成输出文件名
                        var count = originalImagePath.Split("\\");
                        var className = count[count.Length - 2];
                        string outputPath = Path.Combine(outputDirectory, $"augmented_{i}_{Guid.NewGuid()}_CLASS_{index}({className})_CLASS_.jpg");

                        // 保存增强后的图像
                        augmentedImage.Save(outputPath);
                        augmentedImagePaths.Add(outputPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"图像增强过程中发生错误：{ex.Message}");
                // 可以选择抛出异常或者返回空列表
                return new List<string>();
            }

            return augmentedImagePaths;
        }
        
        /// <summary>
        /// 对输入图像进行数据增强
        /// </summary>
        public static List<string> AugmentImage(int index, string originalImagePath, string outputDirectory, int augmentationCount = 10)
        {
            List<string> augmentedImagePaths = [];

            // 确保输出目录存在
            Directory.CreateDirectory(outputDirectory);

            // 随机数生成器
            Random random = new Random();

            // 使用 SkiaSharp 加载原始图像
            using (SKBitmap originalBitmap = SKBitmap.Decode(originalImagePath))
            {
                for (int i = 0; i < augmentationCount; i++)
                {
                    // 创建一个可变副本
                    using (SKSurface surface = SKSurface.Create(new SKImageInfo(originalBitmap.Width, originalBitmap.Height)))
                    {
                        SKCanvas canvas = surface.Canvas;
                        SKPaint paint = new SKPaint();

                        // 绘制原始图像
                        canvas.DrawBitmap(originalBitmap, 0, 0);

                        // 1. 随机水平翻转
                        if (random.NextDouble() > 0.5)
                        {
                            canvas.Scale(-1, 1, originalBitmap.Width / 2f, originalBitmap.Height / 2f);
                            canvas.DrawBitmap(originalBitmap, 0, 0);
                        }

                        // 2. 随机垂直翻转
                        if (random.NextDouble() > 0.5)
                        {
                            canvas.Scale(1, -1, originalBitmap.Width / 2f, originalBitmap.Height / 2f);
                            canvas.DrawBitmap(originalBitmap, 0, 0);
                        }

                        // 3. 亮度和对比度调整
                        float brightness = (float)(0.9 + random.NextDouble() * 0.2);  // 0.9 to 1.1
                        float contrastAmount = (float)((random.NextDouble() - 0.5) * 0.4);   // -0.2 to 0.2

                        paint.ColorFilter = CreateBrightnessContrastFilter(brightness, contrastAmount);
                        canvas.DrawBitmap(originalBitmap, 0, 0, paint);

                        // 4. 轻微旋转
                        float rotationAngle = (float)((random.NextDouble() - 0.5) * 20); // -10 to 10 度
                        canvas.Save();
                        canvas.Translate(originalBitmap.Width / 2f, originalBitmap.Height / 2f);
                        canvas.RotateDegrees(rotationAngle);
                        canvas.Translate(-originalBitmap.Width / 2f, -originalBitmap.Height / 2f);
                        canvas.DrawBitmap(originalBitmap, 0, 0);
                        canvas.Restore();

                        //// 5. 对比度调整（替代饱和度）
                        //float saturationAmount = (float)((random.NextDouble() - 0.5) * 0.4); // -0.2 to 0.2
                        //paint.ColorFilter = Contrast(saturationAmount);
                        //canvas.DrawBitmap(originalBitmap, 0, 0, paint);

                        // 保存增强后的图像
                        SKImage image = surface.Snapshot();
                        SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                        var count = originalImagePath.Split("\\");
                        var className = count[count.Length - 2];
                        string outputPath = Path.Combine(outputDirectory, $"augmented_{i}_{Guid.NewGuid()}_CLASS_{index}({className})_CLASS_.jpg");
                        using (FileStream stream = File.OpenWrite(outputPath))
                        {
                            data.SaveTo(stream);
                        }

                        augmentedImagePaths.Add(outputPath);
                    }
                }
            }

            return augmentedImagePaths;
        }


        /// <summary>
        /// 创建亮度和对比度的颜色过滤器
        /// </summary>
        private static SKColorFilter CreateBrightnessContrastFilter(float brightness, float contrast)
        {
            // 调整亮度和对比度的更安全的颜色矩阵
            float[] colorMatrix = new float[]
            {
                contrast, 0, 0, 0, (1 - contrast) * 128 * brightness,
                0, contrast, 0, 0, (1 - contrast) * 128 * brightness,
                0, 0, contrast, 0, (1 - contrast) * 128 * brightness,
                0, 0, 0, 1, 0
            };

            return SKColorFilter.CreateColorMatrix(colorMatrix);
        }
        /// <summary>
        /// 调整图像对比度。amount 是调整水平。负值降低对比度，正值增加对比度，0 表示无变化。
        /// </summary>
        /// <param name="amount">对比度调整量</param>
        /// <returns>SKColorFilter</returns>
        public static SKColorFilter Contrast(float amount)
        {
            float translatedContrast = amount + 1;
            float averageLuminance = 0.5f * (1 - amount);

            return SKColorFilter.CreateColorMatrix(new float[]
            {
            translatedContrast, 0, 0, 0, averageLuminance,
            0, translatedContrast, 0, 0, averageLuminance,
            0, 0, translatedContrast, 0, averageLuminance,
            0, 0, 0, 1, 0
            });
        }
    }
}
