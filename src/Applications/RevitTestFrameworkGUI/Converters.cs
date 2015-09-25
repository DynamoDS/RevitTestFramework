﻿using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RTF.Framework;
using Autodesk.RevitAddIns;

namespace RTF.Applications
{
    public class WorkingPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value ?? "Select a working path...";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }

    public class AssemblyPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value ?? "Select a test assembly path...";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }

    public class ResultsPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value ?? "Select a results path...";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }

    public class StringToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            // Do the conversion from string to int
            int converted = 0;
            try
            {
                converted = Int32.Parse(value.ToString());
            }
            catch { }

            return converted;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            // Do the conversion from int to string
            return value.ToString();
        }
    }

    public class TestStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            var status = (TestStatus)value;
            switch (status)
            {
                case TestStatus.None:
                    return Brushes.Transparent;
                case TestStatus.Cancelled:
                    return new SolidColorBrush(Colors.DarkGray);
                case TestStatus.TimedOut:
                    return new SolidColorBrush(Colors.Orange);
                case TestStatus.Error:
                    return new SolidColorBrush(Colors.OrangeRed);
                case TestStatus.Failure:
                    return new SolidColorBrush(Colors.OrangeRed);
                case TestStatus.Ignored:
                    return Brushes.Transparent;
                case TestStatus.Inconclusive:
                    return new SolidColorBrush(Colors.DarkGray);
                case TestStatus.NotRunnable:
                    return new SolidColorBrush(Colors.DarkGray);
                case TestStatus.Skipped:
                    return Brushes.Transparent;
                case TestStatus.Success:
                    return new SolidColorBrush(Colors.GreenYellow);
                default:
                    return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class FixtureStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            var status = (FixtureStatus)value;
            switch (status)
            {
                case FixtureStatus.None:
                    return Brushes.Transparent;
                case FixtureStatus.Mixed:
                    return new SolidColorBrush(Colors.Orange);
                case FixtureStatus.Failure:
                    return new SolidColorBrush(Colors.OrangeRed);
                case FixtureStatus.Success:
                    return new SolidColorBrush(Colors.GreenYellow);
                default:
                    return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class EmptyStringToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!string.IsNullOrEmpty(value.ToString()))
                return Visibility.Visible;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class BoolInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool) value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class BoolExistsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool) value)
            {
                return new SolidColorBrush(Colors.LightGreen);
            }

            return new SolidColorBrush(Colors.LightPink);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class BoolToVisibilityCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool) value)
            {
                var check = (bool) value;
                return check ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
