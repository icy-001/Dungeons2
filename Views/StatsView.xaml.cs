using System.Windows.Controls;
using System.Windows.Input;
using Dungeons2.ViewModels;

namespace Dungeons2.Views
{
    public partial class StatsView : UserControl
    {
        public StatsView()
        {
            InitializeComponent();
        }

        private void FloorTimeCdfCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Canvas { DataContext: FloorTimeCdfPlot plot } canvas)
                plot.UpdateHover(e.GetPosition(canvas).X);
        }

        private void FloorTimeCdfCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Canvas { DataContext: FloorTimeCdfPlot plot })
                plot.ClearHover();
        }
    }
}
