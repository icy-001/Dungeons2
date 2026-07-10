using System.Globalization;
using System.Windows.Data;

namespace Dungeons2
{
    public class TimeSpanToTotalMinutesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan ts)
                return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
