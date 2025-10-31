using AutoTrainer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace AutoTrainer.Helpers
{
    public static class AnnotationFileHelper
    {
        public static (int successCount, int errorCount, List<string> newClasses) ImportClassifications(
            string filePath,
            ObservableCollection<ImageItem> imageList,
            ObservableCollection<string> existingClassNames)
        {
            var successCount = 0;
            var errorCount = 0;
            var newClasses = new List<string>();
            var imageLookup = imageList.ToDictionary(img => img.FileName.Split('.')[0], img => img);

            try
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    var parts = line.Trim().Split(new[] { ' ', '\t'}, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var imageName = parts[0];
                        var className = parts[1];

                        if (imageLookup.TryGetValue(imageName, out var imageItem))
                        {
                            if (!imageItem.ImageClasses.Contains(className))
                            {
                                imageItem.ImageClasses.Add(className);
                            }
                            
                            if (!existingClassNames.Contains(className) && !newClasses.Contains(className))
                            {
                                newClasses.Add(className);
                            }
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                        }
                    }
                    else
                    {
                        errorCount++;
                    }
                }
            }
            catch (Exception)
            {
                return (-1, -1, []);
            }

            return (successCount, errorCount, newClasses);
        }
    }
}