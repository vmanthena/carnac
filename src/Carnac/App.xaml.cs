using System;
using System.Reactive.Linq;
using System.Windows;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Carnac.UI;
using Carnac.Utilities;
using SettingsProviderNet;
using Squirrel;
using EventHook;
using EventHook.Hooks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Drawing;

namespace Carnac
{
    public partial class App
    {
        readonly SettingsProvider settingsProvider;
        readonly KeyProvider keyProvider;
        readonly IMessageProvider messageProvider;
        readonly PopupSettings settings;
        KeyShowView keyShowView;
        CarnacTrayIcon trayIcon;
        KeysController carnac;

#if !DEBUG
        readonly string carnacUpdateUrl = "https://github.com/Code52/carnac";
#endif

        public App()
        {
            keyProvider = new KeyProvider(InterceptKeys.Current, new PasswordModeService(), new DesktopLockEventService());
            settingsProvider = new SettingsProvider(new RoamingAppDataStorage("Carnac"));
            settings = settingsProvider.GetSettings<PopupSettings>();
            messageProvider = new MessageProvider(new ShortcutProvider(), keyProvider, settings);
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


        protected override void OnStartup(StartupEventArgs e)
        {
            // Check if there was instance before this. If there was-close the current one.
            if (ProcessUtilities.ThisProcessIsAlreadyRunning())
            {
                ProcessUtilities.SetFocusToPreviousInstance("Carnac");
                Shutdown();
                return;
            }

            trayIcon = new CarnacTrayIcon();
            trayIcon.OpenPreferences += TrayIconOnOpenPreferences;
            var keyShowViewModel = new KeyShowViewModel(settings);

            keyShowView = new KeyShowView(keyShowViewModel);
            keyShowView.Show();

            carnac = new KeysController(keyShowViewModel.Messages, messageProvider, new ConcurrencyService(), settingsProvider);
            carnac.Start();

            MouseWatcher.OnMouseInput += (s, me) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!keyShowView.IsVisible) return;

                    WindowInfo wi = new WindowInfo(new IntPtr(GetForegroundWindow()));

                    Debug.WriteLine(wi.ClassName);
                    Debug.WriteLine(string.Format("{0}, {1}", me.Point.x, me.Point.y));
                    Debug.WriteLine(wi.Rect);
                    var relativePoint = new System.Windows.Point(me.Point.x - wi.Rect.X, me.Point.y - wi.Rect.Y);
                    Debug.WriteLine(relativePoint);
                    var window = keyShowView.PointFromScreen(new System.Windows.Point(me.Point.x, me.Point.y));
                    var rect = keyShowView.PointFromScreen(new System.Windows.Point(wi.Rect.X, wi.Rect.Y));
                    keyShowViewModel.CursorPosition = new System.Windows.Point(window.X - rect.X, window.Y - rect.Y);
                    Debug.WriteLine(keyShowViewModel.CursorPosition);

                    

                    if (me.Message == MouseMessages.WM_LBUTTONUP)
                    {
                        /*
                        var key = new KeyPress(
                        new ProcessInfo("code.exe"), new InterceptKeyEventArgs(System.Windows.Forms.Keys.LButton, KeyDirection.Unknown, true, false, false), false, new List<string>() { "click" }
                        );
                        key.
                        keyProvider.GetKeyStream().OnNext(key);
                        */
                        keyShowView.LeftClick();

                    }
                    if (me.Message == MouseMessages.WM_RBUTTONUP)
                    {
                        keyShowView.RightClick();
                    }
                });
            };

            if (settings.ShowMouseClicks)
            {
                MouseWatcher.Start();
            }
            settings.PropertyChanged += (s, se) => {
                switch (se.PropertyName)
                {
                    case "ShowMouseClicks":
                        if (this.settings.ShowMouseClicks) {
                            MouseWatcher.Start();
                        } else {
                            MouseWatcher.Stop();
                        }
                        break;
                }
            };

#if !DEBUG
            Observable
                .Timer(TimeSpan.FromMinutes(5))
                .Subscribe(async x =>
                {
                    try
                    {
                        using (var mgr = UpdateManager.GitHubUpdateManager(carnacUpdateUrl))
                        {
                            await mgr.Result.UpdateApp();
                        }
        }
                    catch
                    {
                        // Do something useful with the exception
                    }
                });
#endif

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            trayIcon.Dispose();
            carnac.Dispose();
            ProcessUtilities.DestroyMutex();

            base.OnExit(e);
        }

        void TrayIconOnOpenPreferences()
        {
            var preferencesViewModel = new PreferencesViewModel(settingsProvider, new ScreenManager());
            var preferencesView = new PreferencesView(preferencesViewModel);
            preferencesView.Show();
        }
    }
}
