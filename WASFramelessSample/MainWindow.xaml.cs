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

            // 1. ��һ�ַ�����������ƶ�����λ��
            // Content ֻ�о���Ԫ�أ����� transparent �⣩���Ҳ��ᱻ�̵������¼��ĲŻᴥ��
            //var _ = new DragMoveHelper(this.Content, this, winManager);

            // 2. �ڶ��ַ�ʽʹ����Ϣ�¼� SC_MOVE HTCAPTION ������Ϊ���ƶ�����
            UIElement root = (UIElement)this.Content;
            root.PointerPressed += Root_PointerPressed;
            root.PointerReleased += Root_PointerReleased;

            // 3. �����ַ�����Client�������������������Ϳ���ֱ���϶�������
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(this.Content);
            //winManager.IsMaximizable = false;
            //root.IsDoubleTapEnabled = false;
            //this.AppWindow.TitleBar.SetDragRectangles([new RectInt32(0, 0, 220, 32)]);
            // ���Կ��� hook ��ֹ������˫���Ŵ� WM_NCLBUTTONDBLCLK
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
        /// �� WAS ����Ҫ�����꣬�����ͷź󣬵�һ����Ϣ�Żᱻ�������ڶ��ε����꣬���ͷź󣬵ڶ�����Ϣ WM_LBUTTONUP �Żᱻ������
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
        /// ����λ���ƶ����ڣ������Ч��û�¼��ã���Ҫ��֡�������֤����cpuռ�������%4 %5�����¼���ʽ��һ����GCҲ�����Ƶ��һ������ѹ�� �����£���
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
                        PInvoke.GetCursorPos(out var pt); // pointerPoint.Position ��ȡ���ָ��λ�ûᵼ����˸������
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
