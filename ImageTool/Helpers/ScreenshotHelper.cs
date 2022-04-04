﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using ImageTool.Common;
using ImageTool.ViewModels;
using static ImageTool.Interop.NativeMethods;

namespace ImageTool.Helpers
{
    public static class ScreenshotHelper
    {
        public static void StartScreenshot()
        {
            var monitorInfos = MonitorHelper.GetMonitorInfos();

            var detector = new RectDetector();
            detector.Snapshot(monitorInfos
                .Select(item => item.PhysicalScreenRect)
                .Aggregate((acc, item) => acc.Union(item)));

            var mouseEventSource = CreateMouseEventSource();

            foreach (var monitorInfo in monitorInfos)
            {
                var vm = new ViewModelBase(
                    mouseEventSource,
                    CaptureScreen(monitorInfo.PhysicalScreenRect),
                    monitorInfo,
                    detector);

                var window = new MainWindow { DataContext = vm };
                SetWindowRect(window, monitorInfo.PhysicalScreenRect);
                window.Loaded += WindowOnLoaded;
                window.Show();

                vm.Initialize();
            }
        }

        private static IObservable<(MouseMessage message, int x, int y)> CreateMouseEventSource()
        {
            var hotSource = Observable.Create<(MouseMessage message, int x, int y)>(o =>
                    Win32Helper.SubscribeMouseHook((message, info) =>
                    {
                        var p = Win32Helper.GetPhysicalMousePosition();
                        o.OnNext((message, p.X, p.Y));
                    }))
                .ObserveOn(NewThreadScheduler.Default)
                .Publish();

            var disposable = hotSource.Connect();
            SharedProperties.Disposables.Push(disposable);

            return hotSource;
        }

        // For DEBUG
        private static void WindowOnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window &&
                window.DataContext is ViewModelBase vm)
            {
                var source = PresentationSource.FromVisual(window);
                if (source?.CompositionTarget != null)
                {
                    var dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                    var dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                    vm.DpiString = $"{dpiX}, {dpiY}";

                    //(vm.ScaleX, vm.ScaleY) = MonitorHelper.GetScaleFactorFromWindow(new WindowInteropHelper(window).EnsureHandle());
                }
            }
        }

        private static void SetWindowRect(Window window, ReadOnlyRect rect)
        {
            SetWindowPos(
                window.GetHandle(),
                (IntPtr)HWND_TOPMOST,
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                SWP_NOZORDER);
        }

        public static BitmapSource CaptureScreen(MonitorInfo monitor)
        {
            return CaptureScreen(monitor.PhysicalScreenRect);
        }

        public static BitmapSource CaptureScreen(ReadOnlyRect rect)
        {
            var hdcSrc = GetAllMonitorsDC();

            var width = rect.Width;
            var height = rect.Height;
            var hdcDest = CreateCompatibleDC(hdcSrc);
            var hBitmap = CreateCompatibleBitmap(hdcSrc, width, height);
            _ = SelectObject(hdcDest, hBitmap);

            BitBlt(hdcDest, 0, 0, width, height, hdcSrc, rect.X, rect.Y,
                TernaryRasterOperations.SRCCOPY | TernaryRasterOperations.CAPTUREBLT);

            var image = System.Drawing.Image.FromHbitmap(hBitmap);
            var bitmap = image.ToBitmapSource();
            //bitmap = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
            //    BitmapSizeOptions.FromEmptyOptions());

            DeleteObject(hBitmap);
            DeleteDC(hdcDest);
            DeleteDC(hdcSrc);

            return bitmap;
        }

        public static BitmapSource ToBitmapSource(this System.Drawing.Image image)
        {
            using var memoryStream = new MemoryStream();
            image.Save(memoryStream, ImageFormat.Png);
            memoryStream.Seek(0, SeekOrigin.Begin);

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memoryStream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }

        public static IntPtr GetAllMonitorsDC()
        {
            return CreateDC("DISPLAY", null, null, IntPtr.Zero);
        }
    }
}