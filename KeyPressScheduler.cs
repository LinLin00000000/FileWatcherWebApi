using System.Runtime.InteropServices;
using System.Text;
using WindowsInput.Events;

public class KeyPressScheduler {
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    private readonly Random random = new();
    private bool IsEnabled { get; set; } = false;
    private readonly KeyCode triggerKey;
    private readonly string targetWindowTitle;
    private readonly int interval;
    private readonly int jitter;

    // KeyCode: See https://learn.microsoft.com/zh-cn/dotnet/api/system.windows.forms.keys
    public KeyPressScheduler(KeyCode toggleKey, KeyCode triggerKey, string targetWindowTitle, int interval, int jitter, ILogger<FileWatcherWebApi.Program>? logger) {
        this.triggerKey = triggerKey;
        this.targetWindowTitle = targetWindowTitle;
        this.interval = interval;
        this.jitter = jitter;

        // 注册快捷键的事件处理程序
        var Keyboard = WindowsInput.Capture.Global.KeyboardAsync();
        Keyboard.KeyEvent += (sender, e) => {
            if (e.Data?.KeyDown?.Key == toggleKey) {
                if (IsTargetWindowActive()) {
                    IsEnabled = !IsEnabled;
                    if (logger != null) {
                        logger.LogInformation("KeyPressScheduler {Status}", IsEnabled ? "enabled" : "disabled");
                    } else {
                        Console.WriteLine("KeyPressScheduler " + (IsEnabled ? "enabled" : "disabled"));
                    }

                }
            }
        };
    }

    private bool IsTargetWindowActive() {
        IntPtr foregroundWindow = GetForegroundWindow();
        StringBuilder windowTitle = new(256);
        GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);
        // Console.WriteLine(windowTitle);

        return windowTitle.ToString() == targetWindowTitle;
    }

    public void Start() {
        Task.Run(RunSchedulerAsync);
    }

    private async Task RunSchedulerAsync() {
        while (true) {
            if (IsEnabled && IsTargetWindowActive()) {
                // Console.WriteLine("Should Press Key: " + triggerKey);
                await SimulateKeyPressAsync(triggerKey);
            }

            Thread.Sleep(interval + random.Next(-jitter, jitter));
        }
    }

    private static async Task SimulateKeyPressAsync(KeyCode key) {
        await WindowsInput.Simulate.Events()
            .Click(key)
            .Invoke();
    }

    public static void Test() {
        Console.WriteLine("Test!");

        KeyCode toggleKey = KeyCode.O; // 指定用于启用/停用的快捷键
        KeyCode triggerKey = KeyCode.A; // 指定触发键
        string targetWindowTitle = "微信"; // 微信 or EscapeFromTarkov
        int interval = 2000; // 指定的时间间隔（毫秒）
        int jitter = 500; // 抖动时间

        KeyPressScheduler scheduler = new(toggleKey, triggerKey, targetWindowTitle, interval, jitter, null);
        scheduler.Start();
        Console.WriteLine("Start!");
    }
}
