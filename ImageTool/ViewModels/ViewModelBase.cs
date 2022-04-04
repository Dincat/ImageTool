﻿using ImageTool.Common;
using ImageTool.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageTool.ViewModels
{
    internal class ViewModelBase : BindableBase
    {
        private static readonly byte[] SampleBytes = new byte[4];

        private readonly RectDetector _detector;
        private string _dpiString = string.Empty;

        public string DpiString
        {
            get => _dpiString;
            set => SetProperty(ref _dpiString, value);
        }

        public Func<int, int, Color> ColorGetter { get; }

        public BitmapSource Background { get; }

        public MonitorInfo MonitorInfo { get; }

        public ICommand CloseCommand { get; } = new RelayCommand(() => Application.Current.Shutdown());

        public ScreenshotState State { get; }

        public ViewModelBase(
            IObservable<(MouseMessage message, int x, int y)> mouseEventSource,
            BitmapSource background,
            MonitorInfo monitorInfo,
            RectDetector detector)
        {
            State = new ScreenshotState(DetectRectFromPhysicalPoint);
            Background = background;
            MonitorInfo = monitorInfo;
            _detector = detector;

            var disposable = mouseEventSource
                .Subscribe(i => State.PushState(i.message, i.x, i.y));

            ColorGetter = GetColorByCoordinate;

            SharedProperties.Disposables.Push(disposable);
        }

        public void Initialize()
        {
            var initPoint = Win32Helper.GetPhysicalMousePosition();
            State.PushState(MouseMessage.MouseMove, initPoint.X, initPoint.Y);
        }

        private Color GetColorByCoordinate(int x, int y)
        {
            if (x < 0 || x >= Background.PixelWidth ||
                y < 0 || y >= Background.PixelHeight) return Colors.Transparent;

            Background.CopyPixels(new Int32Rect(x, y, 1, 1), SampleBytes, 4, 0);

            return Color.FromArgb(SampleBytes[3], SampleBytes[2], SampleBytes[1], SampleBytes[0]);
        }

        private ReadOnlyRect DetectRectFromPhysicalPoint(int physicalX, int physicalY)
        {
            var rect = _detector.GetByPhysicalPoint(physicalX, physicalY);
            return rect != ReadOnlyRect.Empty && MonitorInfo.PhysicalScreenRect.IntersectsWith(rect)
                ? rect
                : ReadOnlyRect.Zero;
        }
    }
}
