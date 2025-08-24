using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdleForceTray;

public class Settings
{
    public string Mode { get; set; } = "Sleep";
    public int TimeoutMinutes { get; set; } = 20;
    public int CheckIntervalSeconds { get; set; } = 5;
    public bool GuaranteedSleep { get; set; } = false;
    public bool StartWithWindows { get; set; } = true;
    public string LogLevel { get; set; } = "Info";
    public bool FirstRunCompleted { get; set; } = false;

    public void ValidateAndCorrect()
    {
        if (Mode != "Sleep" && Mode != "Shutdown")
            Mode = "Sleep";
        
        if (TimeoutMinutes < 1 || TimeoutMinutes > 1440) // 1 minute to 24 hours
            TimeoutMinutes = 20;
        
        if (CheckIntervalSeconds < 1 || CheckIntervalSeconds > 60)
            CheckIntervalSeconds = 5;
        
        if (LogLevel != "Error" && LogLevel != "Warn" && LogLevel != "Info" && LogLevel != "Debug")
            LogLevel = "Info";
    }
}

public static class SettingsManager
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "IdleForce");
    
    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");
    
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static Settings LoadSettings()
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            // Load settings if file exists
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions) ?? new Settings();
                settings.ValidateAndCorrect();
                
                // Save corrected settings back if validation changed anything
                SaveSettings(settings);
                return settings;
            }
        }
        catch (Exception)
        {
            // If loading fails, return defaults and save them
        }

        // Return defaults and save them
        var defaultSettings = new Settings();
        SaveSettings(defaultSettings);
        return defaultSettings;
    }

    public static void SaveSettings(Settings settings)
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            settings.ValidateAndCorrect();
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception)
        {
            // Silently fail - settings just won't persist
        }
    }
}

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

            // Load settings at startup
            var settings = SettingsManager.LoadSettings();

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }    
}