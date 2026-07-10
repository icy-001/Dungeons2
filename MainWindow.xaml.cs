using System.Windows;

namespace Dungeons2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly OverlayWindow _overlayWindow;

        public MainWindow()
        {
            InitializeComponent();

            _overlayWindow = new();
            _overlayWindow.Show();
        }
    }
}
