using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AetherVPN.Models;

namespace AetherVPN.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionState state)
        {
            return state switch
            {
                ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(0x00, 0xB8, 0x94)),     // Green
                ConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(0xFD, 0xCB, 0x6E)),    // Yellow
                ConnectionState.Reconnecting => new SolidColorBrush(Color.FromRgb(0xFD, 0xCB, 0x6E)), // Yellow
                ConnectionState.Disconnecting => new SolidColorBrush(Color.FromRgb(0xFD, 0xCB, 0x6E)),// Yellow
                ConnectionState.Error => new SolidColorBrush(Color.FromRgb(0xE1, 0x70, 0x55)),         // Red
                _ => new SolidColorBrush(Color.FromRgb(0x88, 0x92, 0xA4)),                             // Gray
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionState state)
        {
            return state == ConnectionState.Connected;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}
