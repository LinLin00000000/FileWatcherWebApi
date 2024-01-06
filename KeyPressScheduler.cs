using TinyHotKey;
using System.Runtime.InteropServices;
using System.Text;

public class KeyPressScheduler
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
    private const int KEYEVENTF_KEYUP = 0x2;

    private readonly Random random = new();
    private readonly TinyHotKeyInstance tinyHotKey = new();
    private bool IsEnabled { get; set; } = true;
    private readonly Key triggerKey;
    private readonly string targetWindowTitle;
    private readonly int interval;
    private readonly int jitter;

    // Key: See https://learn.microsoft.com/zh-cn/dotnet/api/system.windows.forms.keys
    public KeyPressScheduler(Key toggleKey, Key triggerKey, string targetWindowTitle, int interval, int jitter)
    {
        this.triggerKey = triggerKey;
        this.targetWindowTitle = targetWindowTitle;
        this.interval = interval;
        this.jitter = jitter;

        // 注册快捷键的事件处理程序
        tinyHotKey.RegisterHotKey(Modifier.Alt, toggleKey, () =>
        {
            if (IsTargetWindowActive())
            {
                IsEnabled = !IsEnabled;
                Console.WriteLine("KeyPressScheduler " + (IsEnabled ? "enabled" : "disabled"));
            }

            return Task.CompletedTask;
        });
    }

    private bool IsTargetWindowActive()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        StringBuilder windowTitle = new(256);
        GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);
        Console.WriteLine(windowTitle);

        return windowTitle.ToString() == targetWindowTitle;
    }

    public void Start()
    {
        Thread keyPressThread = new(new ThreadStart(RunScheduler))
        {
            IsBackground = false
        };
        keyPressThread.Start();

    }

    private void RunScheduler()
    {
        while (true)
        {
            if (IsEnabled && IsTargetWindowActive())
            {
                Console.WriteLine("Should Press Key: " + triggerKey);
                SimulateKeyPress(triggerKey);
            }

            Thread.Sleep(interval + random.Next(-jitter, jitter));
        }
    }

    private void SimulateKeyPress(Key key)
    {
        keybd_event((byte)key, 0, 0, 0);
        keybd_event((byte)key, 0, KEYEVENTF_KEYUP, 0);
    }

    public static void Test()
    {
        Console.WriteLine("Test!");
        
        Key toggleKey = Key.O; // 指定用于启用/停用的快捷键
        Key triggerKey = Key.A; // 指定触发键
        string targetWindowTitle = "微信";
        int interval = 2000; // 指定的时间间隔（毫秒）
        int jitter = 500; // 抖动时间

        KeyPressScheduler scheduler = new(toggleKey, triggerKey, targetWindowTitle, interval, jitter);
        scheduler.Start();
        Console.WriteLine("Start!");
    }
}
