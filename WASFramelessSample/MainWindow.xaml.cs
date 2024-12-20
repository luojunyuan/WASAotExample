using Microsoft.UI.Xaml;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinUIEx;
using WinUIEx.Messaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WASFramelessSample
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly nint hwnd;

        public MainWindow()
        {
            this.InitializeComponent();

            hwnd = this.GetWindowHandle();

            // Make FrameLess borderless
            HwndExtensions.ToggleWindowStyle(hwnd, false, WindowStyle.TiledWindow);

            // Make background transparent
            this.SystemBackdrop = new TransparentTintBackdrop();

            var winManager = WindowManager.Get(this);
            winManager.Width = winManager.Height = 220;

            // 1. 第一种方法计算鼠标移动窗口位置
            // Content 只有具有元素（除了 transparent 外）并且不会被吞掉按下事件的才会触发
            //var _ = new DragMoveHelper(this.Content, this, winManager);

            // 2. 第二种方式使用消息事件 SC_MOVE HTCAPTION 让它以为在移动窗口
            UIElement root = (UIElement)this.Content;
            root.PointerPressed += Root_PointerPressed;
            root.PointerReleased += Root_PointerReleased;

            // 3. 第三种方法把Client区域当作标题栏，这样就可以直接拖动窗口了
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(this.Content);
            // hook 禁止标题栏双击放大 
            var monitor = new WindowMessageMonitor(this);
            monitor.WindowMessageReceived += Monitor_WindowMessageReceived;

            var process = Process.GetProcessesByName("notepad")[0];
            //var process = Process.GetProcessById(14520);
            var gameWindowHandle = process.MainWindowHandle;
            var uiThread = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            //process.Exited +=  (s, e) =>
            //{
            //    // 进程结束依赖游戏结束？❌应该依赖于窗口关闭
            //    uiThread.TryEnqueue(() => Microsoft.UI.Xaml.Application.Current.Exit());
            //};
            RemovePopupAddChildStyle(hwnd);
            PInvoke.SetParent(new(hwnd), new(gameWindowHandle));
            //PInvoke.GetClientRect(new(gameWindowHandle), out var rectClient);
            //PInvoke.SetWindowPos(new(hwnd), HWND.Null, 0, 0, rectClient.Width, rectClient.Height, SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
            PInvoke.SetWindowPos(new(hwnd), HWND.Null, 0, 0, 220, 220, Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        }

        public static void RemovePopupAddChildStyle(nint hwnd)
        {
            var style = HwndExtensions.GetWindowStyle(hwnd);
            style = style & ~WindowStyle.Popup | WindowStyle.Child;
            HwndExtensions.SetWindowStyle(hwnd, style);
        }

        private void Monitor_WindowMessageReceived(object? sender, WindowMessageEventArgs e)
        {
            const int WM_NCLBUTTONDBLCLK = 0x00A3;
            if (e.Message.MessageId == WM_NCLBUTTONDBLCLK)
            {
                e.Handled = true;
            }
            const int WM_CLOSE = 0x0010;
            const int WM_Destory = 0x0002;
            const int WM_NCDestory = 130;
            if (e.Message.MessageId == WM_Destory)
            {
                Debug.WriteLine("WM_Destory");
                Microsoft.UI.Xaml.Application.Current.Exit();
            }
            if (e.Message.MessageId == WM_NCDestory)
            {
                Debug.WriteLine("WM_NCDestory");
            }
        }

        private void Root_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Root_PointerReleased");
        }

        private void Root_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("Pointer Pressed");
            //DragMove();
            //System.Diagnostics.Debug.WriteLine("Pointer Pressed Leave");
            System.Diagnostics.Debug.WriteLine("Root_PointerPressed");
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            //myButton.Content = "Clicked";
            //Close();
            //CoreApplication.Exit();
            //Microsoft.UI.Xaml.Window.Current.Close();
            Application.Current.Exit();
        }

        enum WindowsMessages : uint
        {
            WM_SYSCOMMAND = 274u,
            WM_LBUTTONUP = 514u,
        }

        /// <summary>
        /// 在 WAS 中需要点击鼠标，并且释放后，第一个消息才会被触发。第二次点击鼠标，再释放后，第二个消息 WM_LBUTTONUP 才会被触发。
        /// </summary>
        private void DragMove()
        {
            // 61458 == SC_MOVE + HTCAPTION
            const uint SC_MOVE = 0xF010;
            const uint HTCAPTION = 0x0002;

            //if (WindowManager.Get(this).WindowState == WindowState.Normal)
            {
                PInvoke.SendMessage(new HWND(hwnd), (uint)WindowsMessages.WM_SYSCOMMAND, SC_MOVE + HTCAPTION, nint.Zero);
                PInvoke.SendMessage(new HWND(hwnd), (uint)WindowsMessages.WM_LBUTTONUP, 0, nint.Zero);
            }
        }

        /// <summary>
        /// 计算位置移动窗口，体感上效率没事件好（需要用帧数软件验证）。cpu占用最多至%4 %5，比事件方式多一倍，GC也会最多频繁一倍（高压力 条件下）。
        /// </summary>
        class DragMoveHelper
        {
            public DragMoveHelper(UIElement root, Window window, WindowManager winManager)
            {
                bool moving = false;
                int initWindowX = 0, initWindowY = 0;
                double initCursorX = 0, initCursorY = 0;

                root.PointerPressed += (s, e) =>
                {
                    if (s is not UIElement sender)
                        return;

                    var pointerPoint = e.GetCurrentPoint(sender);
                    if (pointerPoint.Properties.IsLeftButtonPressed)
                    {
                        sender.CapturePointer(e.Pointer);
                        initWindowX = winManager.AppWindow.Position.X;
                        initWindowY = winManager.AppWindow.Position.Y;
                        PInvoke.GetCursorPos(out var pt); // pointerPoint.Position 获取鼠标指针位置会导致闪烁不定。
                        initCursorX = pt.X;
                        initCursorY = pt.Y;
                        moving = true;
                    }
                };
                root.PointerMoved += (s, e) =>
                {
                    if (s is not UIElement sender)
                        return;

                    var pointerPoint = e.GetCurrentPoint(sender);
                    if (pointerPoint.Properties.IsLeftButtonPressed)
                    {
                        if (moving)
                        {
                            PInvoke.GetCursorPos(out var pt);
                            window.Move(initWindowX + (int)(pt.X - initCursorX), initWindowY + (int)(pt.Y - initCursorY));

                            e.Handled = true;
                        }
                    };
                    root.PointerReleased += (s, e) =>
                    {
                        if (s is not UIElement sender)
                            return;

                        sender.ReleasePointerCaptures();
                        moving = false;
                    };
                };
            }
        }
    }
}
