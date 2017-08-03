﻿using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using JetBrains.Annotations;
using Remembrance.Card.ViewModel.Contracts.Data;
using Scar.Common.WPF.Converters;

namespace Remembrance.Card.View.Converters
{
    [ValueConversion(typeof(WordViewModel), typeof(Visibility))]
    public sealed class PriorityWordViewModelToVisibilityConverter : IValueConverter
    {
        [NotNull]
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is PriorityWordViewModel)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(WordViewModel), typeof(bool))]
    public sealed class PriorityWordViewModelToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is PriorityWordViewModel;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}