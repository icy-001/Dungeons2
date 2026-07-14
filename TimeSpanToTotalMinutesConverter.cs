using System.Globalization;
using System.Windows.Data;

namespace Dungeons2
{
    public class TimeSpanToTotalMinutesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is TimeSpan ts ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}" : string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
