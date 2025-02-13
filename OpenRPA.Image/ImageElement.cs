﻿using OpenRPA.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace OpenRPA.Image
{
    public class ImageElement : IElement, IDisposable
    {
        public string Name { get; set; }
        public string Processname { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Text { get; set; }
        public float Confidence { get; set; }
        private System.Drawing.Bitmap _element = null;
        public System.Drawing.Bitmap element
        {
            get
            {
                if (_element == null)
                {
                    _element = Interfaces.Image.Util.Screenshot(Rectangle.X, Rectangle.Y, Rectangle.Width, Rectangle.Height);
                }
                return _element;
            }
            set
            {
                _element = value;
            }
        }
        object IElement.RawElement { get => element; set => element = value as System.Drawing.Bitmap; }
        public System.Drawing.Rectangle Rectangle
        {
            get
            {
                return new System.Drawing.Rectangle(X, Y, Width, Height);
            }
            set
            {
                X = value.X;
                Y = value.Y;
                Width = value.Width;
                Height = value.Height;
            }
        }
        public ImageElement(System.Drawing.Rectangle Rectangle)
        {
            X = Rectangle.X;
            Y = Rectangle.Y;
            Width = Rectangle.Width;
            Height = Rectangle.Height;
        }
        public ImageElement(System.Drawing.Rectangle Rectangle, System.Drawing.Bitmap Element)
        {
            X = Rectangle.X;
            Y = Rectangle.Y;
            Width = Rectangle.Width;
            Height = Rectangle.Height;
            this.element = Element;
        }
        public void Click(bool VirtualClick, Input.MouseButton Button, int OffsetX, int OffsetY, bool DoubleClick, bool AnimateMouse)
        {
            if (AnimateMouse)
            {
                Input.InputDriver.Instance.AnimateMouseMove(Rectangle.X + OffsetX, Rectangle.Y + OffsetY);
            }
            else
            {
                Input.InputDriver.Instance.MouseMove(Rectangle.X + OffsetX, Rectangle.Y + OffsetY);
            }
            Input.InputDriver.Click(Button);
            if (DoubleClick) Input.InputDriver.Click(Button);
        }
        public void Focus()
        {
            throw new NotImplementedException();
        }
        public Task Highlight(bool Blocking, System.Drawing.Color Color, TimeSpan Duration)
        {
            if (!Blocking)
            {
                Task.Run(() => _Highlight(Color, Duration));
                return Task.CompletedTask;
            }
            return _Highlight(Color, Duration);
        }
        [DllImport("User32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("User32.dll")]
        public static extern void ReleaseDC(IntPtr hwnd, IntPtr dc);
        public Task _Highlight(System.Drawing.Color Color, TimeSpan Duration)
        {
            System.Threading.Thread.CurrentThread.Name = "UIHighlighter";
            using (Interfaces.Overlay.OverlayWindow _overlayWindow = new Interfaces.Overlay.OverlayWindow(true))
            {
                _overlayWindow.BackColor = Color;
                _overlayWindow.Visible = true;
                //_overlayWindow.SetTimeout(Duration);
                _overlayWindow.Bounds = Rectangle;
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                do
                {
                    System.Threading.Thread.Sleep(10);
                    _overlayWindow.TopMost = true;
                } while (_overlayWindow.Visible && sw.Elapsed < Duration);
                _overlayWindow.Close();
                return Task.CompletedTask;
            }
            //var r = new Rectangle(Rectangle.Location, Rectangle.Size);
            //IntPtr desktopPtr = GetDC(IntPtr.Zero);
            //IntPtr hWnd = rectutil.WindowFromPoint(r.Location);
            //try
            //{
            //    using (Graphics g = Graphics.FromHdc(desktopPtr))
            //    {
            //        SolidBrush b = new SolidBrush(Color);
            //        Pen p = new Pen(Color, 5);
            //        var sw = new System.Diagnostics.Stopwatch();
            //        sw.Start();
            //        do
            //        {
            //            g.DrawRectangle(p, r);
            //            // System.Threading.Thread.Sleep(1);
            //            // await Task.Delay(1);
            //            // g.DrawEllipse(p, r);
            //            // g.FillRectangle(b, r);
            //        } while (sw.Elapsed < Duration);
            //        sw.Stop();
            //        // await Task.Delay(Duration);
            //    }
            //    // rectutil.InvalidateRect(hWnd, IntPtr.Zero, true);
            //    rectutil.InvalidateRect(IntPtr.Zero, r, true);
            //}
            //finally
            //{
            //    ReleaseDC(IntPtr.Zero, desktopPtr);
            //}
        }
        public string ImageString()
        {
            if (element == null)
            {
                var AddedWidth = 10;
                var AddedHeight = 10;
                var ScreenImageWidth = Rectangle.Width + AddedWidth;
                var ScreenImageHeight = Rectangle.Height + AddedHeight;
                var ScreenImagex = Rectangle.X - (AddedWidth / 2);
                var ScreenImagey = Rectangle.Y - (AddedHeight / 2);
                if (ScreenImagex < 0) ScreenImagex = 0; if (ScreenImagey < 0) ScreenImagey = 0;
                using (var image = Interfaces.Image.Util.Screenshot(ScreenImagex, ScreenImagey, ScreenImageWidth, ScreenImageHeight, Interfaces.Image.Util.ActivityPreviewImageWidth, Interfaces.Image.Util.ActivityPreviewImageHeight))
                {
                    return Interfaces.Image.Util.Bitmap2Base64(image);
                }
            }
            else
            {
                return Interfaces.Image.Util.Bitmap2Base64(element);
            }
        }
        public void Dispose()
        {
            if (_element != null) _element.Dispose();
        }
        private Emgu.CV.OCR.Tesseract _ocr;
        public string Value
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(Text))
                    {
                        return Text;
                    }
                    var lang = Config.local.ocrlanguage;
                    string basepath = Interfaces.Extensions.DataDirectory;
                    string path = System.IO.Path.Combine(basepath, "tessdata");
                    ocr.TesseractDownloadLangFile(path, Config.local.ocrlanguage);
                    ocr.TesseractDownloadLangFile(path, "osd");
                    _ocr = new Emgu.CV.OCR.Tesseract(path, lang.ToString(), Emgu.CV.OCR.OcrEngineMode.TesseractLstmCombined);
                    _ocr.Init(path, lang.ToString(), Emgu.CV.OCR.OcrEngineMode.TesseractLstmCombined);
                    _ocr.PageSegMode = Emgu.CV.OCR.PageSegMode.SparseText;

                    // OpenRPA.Interfaces.Image.Util.SaveImageStamped(element, "OCR");
                    using (var img = new Emgu.CV.Image<Emgu.CV.Structure.Bgr, byte>(element))
                    {
                        return ocr.OcrImage(_ocr, img.Mat);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                    return null;
                }
            }
            set
            {
                Text = value;
            }
        }
        public IElement[] Items
        {
            get
            {
                return new IElement[] { };
            }
        }
        public bool SaveAs(string filename)
        {
            try
            {
                element.Save(filename);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
            return false;
        }

    }
}