using System.Runtime.InteropServices;

namespace IdleForceTray;

static class Program
{
    #region Windows API Declarations

    // Structure for GetLastInputInfo
    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    // Structure for XInput controller state
    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    // GetLastInputInfo - Gets time of last input event
    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    // GetTickCount - Gets system uptime in milliseconds
    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();

    // SetSuspendState - Forces system to sleep/hibernate
    [DllImport("powrprof.dll", SetLastError = true)]
    public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    // XInput API declarations - try multiple DLL versions
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState14(uint dwUserIndex, ref XINPUT_STATE pState);

    [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState13(uint dwUserIndex, ref XINPUT_STATE pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState910(uint dwUserIndex, ref XINPUT_STATE pState);

    // XInput wrapper that tries different DLL versions
    public static uint XInputGetState(uint dwUserIndex, ref XINPUT_STATE pState)
    {
        try
        {
            return XInputGetState14(dwUserIndex, ref pState);
        }
        catch (DllNotFoundException)
        {
            try
            {
                return XInputGetState13(dwUserIndex, ref pState);
            }
            catch (DllNotFoundException)
            {
                try
                {
                    return XInputGetState910(dwUserIndex, ref pState);
                }
                catch (DllNotFoundException)
                {
                    // No XInput DLL available
                    return 1167; // ERROR_DEVICE_NOT_CONNECTED
                }
            }
        }
    }

    // Helper method to get last input time safely
    public static uint GetLastInputTime()
    {
        try
        {
            LASTINPUTINFO lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            
            if (GetLastInputInfo(ref lastInput))
            {
                return lastInput.dwTime;
            }
            return 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    // Helper method to force system sleep safely
    public static bool ForceSleep(bool hibernate = false, bool force = true)
    {
        try
        {
            return SetSuspendState(hibernate, force, false);
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Single instance check using named mutex
        using (var mutex = new Mutex(true, @"Global\IdleForceTray", out bool createdNew))
        {
            if (!createdNew)
            {
                // Another instance is already running, exit silently
                return;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }    
}