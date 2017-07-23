using System;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Carnac.Logic;
using Gma.System.MouseKeyHook;
using System.Drawing;
using System.Text;

namespace Carnac.UI
{
    public partial class KeyShowView: IDisposable
    {
        private Storyboard sb;
        IKeyboardMouseEvents m_GlobalHook = null;

        public KeyShowView(KeyShowViewModel keyShowViewModel)
        {
            DataContext = keyShowViewModel;
            InitializeComponent();
            keyShowViewModel.Settings.PropertyChanged += Settings_PropertyChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            /*
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32Methods.SetWindowExTransparentAndNotInWindowList(hwnd);
            var timer = new Timer(100);
            timer.Elapsed +=
                (s, x) =>
                {
                    SetWindowPos(hwnd,
                                 HWND.TOPMOST,
                                 0, 0, 0, 0,
                                 (uint)(SWP.NOMOVE | SWP.NOSIZE | SWP.SHOWWINDOW));
                };

            timer.Start();
            */
            var vm = ((KeyShowViewModel)DataContext);
            Left = vm.Settings.Left;
            vm.Settings.LeftChanged += SettingsLeftChanged;
            //WindowState = WindowState.Maximized;
            if (vm.Settings.ShowMouseClicks)
            {
                SetupMouseEvents();
            }
        }

        public void Dispose()
        {
            if (m_GlobalHook != null)
            {
                m_GlobalHook.Dispose();
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int W, int H, uint uFlags);


        /// <summary>
        /// HWND values for hWndInsertAfter
        /// </summary>
        public static class HWND
        {
            public static readonly IntPtr
            NOTOPMOST = new IntPtr(-2),
            BROADCAST = new IntPtr(0xffff),
            TOPMOST = new IntPtr(-1),
            TOP = new IntPtr(0),
            BOTTOM = new IntPtr(1);
        }


        /// <summary>
        /// SetWindowPos Flags
        /// </summary>
        public static class SWP
        {
            public static readonly int
            NOSIZE = 0x0001,
            NOMOVE = 0x0002,
            NOZORDER = 0x0004,
            NOREDRAW = 0x0008,
            NOACTIVATE = 0x0010,
            DRAWFRAME = 0x0020,
            FRAMECHANGED = 0x0020,
            SHOWWINDOW = 0x0040,
            HIDEWINDOW = 0x0080,
            NOCOPYBITS = 0x0100,
            NOOWNERZORDER = 0x0200,
            NOREPOSITION = 0x0200,
            NOSENDCHANGING = 0x0400,
            DEFERERASE = 0x2000,
            ASYNCWINDOWPOS = 0x4000;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            sb = this.FindResource("clickHighlighterStoryboard") as Storyboard;
        }

        void SettingsLeftChanged(object sender, EventArgs e)
        {
            WindowState = WindowState.Normal;
            var vm = ((KeyShowViewModel)DataContext);
            Left = vm.Settings.Left;
            //WindowState = WindowState.Maximized;
        }

        void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var vm = ((KeyShowViewModel)DataContext);
            switch (e.PropertyName)
            {
                case "ClickFadeDelay":
                    Duration d = TimeSpan.FromMilliseconds(vm.Settings.ClickFadeDelay);
                    foreach(DoubleAnimation da in sb.Children)
                    {
                        da.Duration = d;
                    }
                    break;
                case "ShowMouseClicks":
                    if (vm.Settings.ShowMouseClicks)
                    {
                        SetupMouseEvents();
                    }
                    else
                    {
                        DestroyMouseEvents();
                    }
                    break;
            }
        }

        void SetupMouseEvents()
        {
            if (m_GlobalHook == null)
            {
                m_GlobalHook = Hook.GlobalEvents();
            }
            m_GlobalHook.MouseDown += OnMouseDown;
            m_GlobalHook.MouseMove += OnMouseMove;
        }

        void DestroyMouseEvents()
        {
            if (m_GlobalHook == null)
            {
                return;
            }
            m_GlobalHook.MouseDown -= OnMouseDown;
            m_GlobalHook.MouseMove -= OnMouseMove;
            m_GlobalHook.Dispose();
            m_GlobalHook = null;
        }

        private void OnMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var vm = ((KeyShowViewModel)DataContext);
            vm.Settings.ClickColor = vm.Settings.LeftClickColor;
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                vm.Settings.ClickColor = vm.Settings.RightClickColor;
            }
            sb.Begin();
        }
        /*
        private void OnMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var vm = ((KeyShowViewModel)DataContext);
            vm.CursorPosition = PointFromScreen(new Point(e.X, e.Y));
        }
        */
        private void OnMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var vm = ((KeyShowViewModel)DataContext);
            WindowInfo wi = new WindowInfo(new IntPtr(GetForegroundWindow()));
            var window = PointFromScreen(new System.Windows.Point(e.X, e.Y));
            var rect = PointFromScreen(new System.Windows.Point(wi.Rect.X, wi.Rect.Y));
            vm.CursorPosition = new System.Windows.Point(window.X - rect.X, window.Y - rect.Y);
        }

        public const int WM_GETTEXT = 0xD;
        public const int WM_GETTEXTLENGTH = 0x000E;


        [DllImport("User32.dll")]
        private static extern int GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr handle, StringBuilder ClassName, int MaxCount);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr handle, int msg, int Param1, int Param2);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr handle, int msg, int Param, System.Text.StringBuilder text);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr handle, out RECT Rect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static string GetWindowClassName(IntPtr handle)
        {
            StringBuilder buffer = new StringBuilder(128);
            GetClassName(handle, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        public static string GetWindowText(IntPtr handle)
        {
            StringBuilder buffer = new StringBuilder(SendMessage(handle, WM_GETTEXTLENGTH, 0, 0) + 1);
            SendMessage(handle, WM_GETTEXT, buffer.Capacity, buffer);
            return buffer.ToString();
        }

        public static Rectangle GetWindowRectangle(IntPtr handle)
        {
            RECT rect = new RECT();
            GetWindowRect(handle, out rect);
            return new Rectangle(rect.Left, rect.Top, (rect.Right - rect.Left) + 1, (rect.Bottom - rect.Top) + 1);
        }

        public class WindowInfo
        {
            public IntPtr Handle;
            public string ClassName;
            public string Text;
            public Rectangle Rect;

            public WindowInfo(IntPtr Handle)
            {
                this.Handle = Handle;
                this.ClassName = GetWindowClassName(Handle);
                this.Text = GetWindowText(Handle);
                this.Rect = GetWindowRectangle(Handle);
            }

            public override string ToString()
            {
                return string.Format("WI: [{0}] {1} {2}", ClassName, Text, Rect);
            }
        }
    }
}
