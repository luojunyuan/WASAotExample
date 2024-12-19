using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinUIEx;

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
            //winManager.IsMaximizable = false;
            //root.IsDoubleTapEnabled = false;
            //this.AppWindow.TitleBar.SetDragRectangles([new RectInt32(0, 0, 220, 32)]);
            // 可以考虑 hook 禁止标题栏双击放大 WM_NCLBUTTONDBLCLK
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
            Close();
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
