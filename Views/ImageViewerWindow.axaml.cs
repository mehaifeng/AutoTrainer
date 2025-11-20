using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace AutoTrainer.Views
{
    public partial class ImageViewerWindow : Window
    {
        public ImageViewerWindow()
        {
            InitializeComponent();
        }

        public void SetImage(Bitmap image)
        {
            ViewImage.Source = image;
        }
    }
}
