using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Text;
using IdleForceTray.Tests;

namespace IdleForceTray;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

public static class Logger
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "IdleForce", "logs");
    
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "IdleForce.log");
    private static readonly object LogLock = new object();
    private const long MaxLogFileSize = 1024 * 1024; // 1 MB
    private const int MaxLogFiles = 3;
    
    private static LogLevel currentLogLevel = LogLevel.Info;
    
    public static void SetLogLevel(LogLevel level)
    {
        currentLogLevel = level;
    }
    
    public static void SetLogLevel(string level)
    {
        if (Enum.TryParse<LogLevel>(level, true, out var logLevel))
        {
            currentLogLevel = logLevel;
        }
    }
    
    public static void Debug(string message)
    {
        Log(LogLevel.Debug, message);
    }
    
    public static void Info(string message)
    {
        Log(LogLevel.Info, message);
    }
    
    public static void Warn(string message)
    {
        Log(LogLevel.Warn, message);
    }
    
    public static void Error(string message)
    {
        Log(LogLevel.Error, message);
    }
    
    public static void Error(string message, Exception ex)
    {
        Log(LogLevel.Error, $"{message}: {ex.Message}");
        Log(LogLevel.Debug, $"Stack trace: {ex.StackTrace}");
    }
    
    private static void Log(LogLevel level, string message)
    {
        if (level < currentLogLevel) return;
        
        try
        {
            lock (LogLock)
            {
                // Ensure directory exists
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
                
                // Check if rotation is needed
                if (File.Exists(LogFilePath))
                {
                    var fileInfo = new FileInfo(LogFilePath);
                    if (fileInfo.Length >= MaxLogFileSize)
                    {
                        RotateLogFiles();
                    }
                }
                
                // Write log entry
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string levelStr = level.ToString().ToUpper().PadRight(5);
                string logEntry = $"[{timestamp}] [{levelStr}] {message}";
                
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                
                // Also output to console for debugging
                Console.WriteLine(logEntry);
            }
        }
        catch (Exception)
        {
            // Logging failed - write to console as fallback
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [LOG ERROR] Failed to write log: {message}");
        }
    }
    
    private static void RotateLogFiles()
    {
        try
        {
            // Delete oldest log file if it exists
            string oldestLogFile = Path.Combine(LogDirectory, $"IdleForce.{MaxLogFiles - 1}.log");
            if (File.Exists(oldestLogFile))
            {
                File.Delete(oldestLogFile);
            }
            
            // Rotate existing log files
            for (int i = MaxLogFiles - 2; i >= 1; i--)
            {
                string sourceFile = Path.Combine(LogDirectory, $"IdleForce.{i}.log");
                string destFile = Path.Combine(LogDirectory, $"IdleForce.{i + 1}.log");
                
                if (File.Exists(sourceFile))
                {
                    File.Move(sourceFile, destFile);
                }
            }
            
            // Move current log file to .1
            string firstRotatedFile = Path.Combine(LogDirectory, "IdleForce.1.log");
            if (File.Exists(LogFilePath))
            {
                File.Move(LogFilePath, firstRotatedFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [LOG ERROR] Failed to rotate log files: {ex.Message}");
        }
    }
}

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
                
                // Initialize logger with settings log level
                Logger.SetLogLevel(settings.LogLevel);
                
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
        Logger.SetLogLevel(defaultSettings.LogLevel);
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
    private static System.Windows.Forms.Timer? activityTimer;
    private static uint lastInputTime = 0;
    private static uint[] lastControllerPackets = new uint[4];
    private static uint lastActivityTime = 0;
    private static Settings? appSettings;
    private static Form1? mainForm;
    private static bool verboseLogging = false;
    
    public static bool IsPaused => mainForm?.IsPaused ?? false;

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

    #region Startup Management
    
    // COM interfaces for creating shell links
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxArgs);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010c-0000-0000-C000-000000000046")]
    internal interface IPersist
    {
        void GetClassID(out Guid pClassID);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    internal interface IPersistFile : IPersist
    {
        new void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();

        [PreserveSig]
        int Load([In, MarshalAs(UnmanagedType.LPWStr)]
            string pszFileName, uint dwMode);

        [PreserveSig]
        int Save([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
            [In, MarshalAs(UnmanagedType.Bool)] bool fRemember);

        [PreserveSig]
        int SaveCompleted([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

        [PreserveSig]
        int GetCurFile([In, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
    }

    public static string GetStartupShortcutPath()
    {
        string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return Path.Combine(startupFolder, "IdleForceTray.lnk");
    }

    public static bool CreateStartupShortcut()
    {
        try
        {
            string shortcutPath = GetStartupShortcutPath();
            string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            
            // Create the shortcut using COM
            IShellLink link = (IShellLink)new ShellLink();
            link.SetDescription("IdleForce Tray - Auto Sleep/Shutdown Tool");
            link.SetPath(exePath);
            link.SetWorkingDirectory(Path.GetDirectoryName(exePath));
            
            IPersistFile file = (IPersistFile)link;
            file.Save(shortcutPath, false);
            
            return File.Exists(shortcutPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool DeleteStartupShortcut()
    {
        try
        {
            string shortcutPath = GetStartupShortcutPath();
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
            return !File.Exists(shortcutPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsStartupShortcutEnabled()
    {
        return File.Exists(GetStartupShortcutPath());
    }

    // Task Scheduler fallback methods
    public static bool CreateStartupTask()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            string taskName = "IdleForceTray";
            
            // Create a scheduled task using schtasks command
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/create /tn \"{taskName}\" /tr \"{exePath}\" /sc onlogon /ru \"{Environment.UserName}\" /f",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                process?.WaitForExit(5000); // Wait up to 5 seconds
                return process?.ExitCode == 0;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool DeleteStartupTask()
    {
        try
        {
            string taskName = "IdleForceTray";
            
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/delete /tn \"{taskName}\" /f",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                process?.WaitForExit(5000); // Wait up to 5 seconds
                return true; // Don't check exit code since task might not exist
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool SetStartupEnabled(bool enabled)
    {
        if (enabled)
        {
            // Try shortcut first, fall back to Task Scheduler
            if (CreateStartupShortcut())
            {
                return true;
            }
            return CreateStartupTask();
        }
        else
        {
            // Remove both shortcut and task if they exist
            bool shortcutRemoved = DeleteStartupShortcut();
            bool taskRemoved = DeleteStartupTask();
            return shortcutRemoved || taskRemoved;
        }
    }

    #endregion

    #region Elevation and Power Management
    
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
    
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
    
    private const int TokenElevationType = 18;
    private const int TokenElevationTypeDefault = 1;
    private const int TokenElevationTypeFull = 2;
    private const int TokenElevationTypeLimited = 3;
    
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            IntPtr tokenHandle = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), 0x0008, out tokenHandle))
                    return false;
                
                IntPtr elevationTypePtr = Marshal.AllocHGlobal(sizeof(int));
                try
                {
                    uint returnLength;
                    if (!GetTokenInformation(tokenHandle, TokenElevationType, elevationTypePtr, (uint)sizeof(int), out returnLength))
                        return false;
                    
                    int elevationType = Marshal.ReadInt32(elevationTypePtr);
                    return elevationType == TokenElevationTypeFull;
                }
                finally
                {
                    Marshal.FreeHGlobal(elevationTypePtr);
                }
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                    CloseHandle(tokenHandle);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to check elevation status", ex);
            return false;
        }
    }
    
    public static bool DisableHibernation()
    {
        try
        {
            Logger.Info("Attempting to disable hibernation using powercfg -h off");
            
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "-h off",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Verb = "runas" // Request elevation
            };
            
            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                if (process != null)
                {
                    process.WaitForExit(10000); // Wait up to 10 seconds
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    if (process.ExitCode == 0)
                    {
                        Logger.Info("Successfully disabled hibernation");
                        return true;
                    }
                    else
                    {
                        Logger.Error($"powercfg -h off failed with exit code {process.ExitCode}. Output: {output}. Error: {error}");
                        return false;
                    }
                }
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled elevation prompt
            Logger.Info("User cancelled elevation prompt for hibernation disable");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to disable hibernation", ex);
            return false;
        }
        
        return false;
    }
    
    public static bool IsHibernationEnabled()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/a",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                if (process != null)
                {
                    process.WaitForExit(5000);
                    string output = process.StandardOutput.ReadToEnd();
                    
                    // Check if hibernate is available in the output
                    return output.Contains("Hibernate", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to check hibernation status", ex);
        }
        
        return false; // Assume disabled if we can't check
    }
    
    #endregion

    private static void ParseCommandLineArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();
            
            switch (arg)
            {
                case "-v":
                case "--verbose":
                case "/v":
                case "/verbose":
                    verboseLogging = true;
                    break;
                    
                case "-h":
                case "--help":
                case "/h":
                case "/help":
                case "/?":
                    ShowHelp();
                    Environment.Exit(0);
                    break;
                    
                case "-t":
                case "--test":
                case "/t":
                case "/test":
                    ElevationTests.RunAllTests();
                    Environment.Exit(0);
                    break;
                    
                default:
                    if (arg.StartsWith("-") || arg.StartsWith("/"))
                    {
                        Console.WriteLine($"Unknown argument: {args[i]}");
                        Console.WriteLine("Use --help for usage information.");
                        Environment.Exit(1);
                    }
                    break;
            }
        }
    }
    
    private static void ShowHelp()
    {
        Console.WriteLine("IdleForce Tray - Auto Sleep/Shutdown Tool");
        Console.WriteLine();
        Console.WriteLine("Usage: IdleForceTray.exe [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --verbose    Enable verbose activity logging (logs every 5 seconds)");
        Console.WriteLine("  -t, --test       Run elevation helper tests (for development/troubleshooting)");
        Console.WriteLine("  -h, --help       Show this help message");
        Console.WriteLine();
        Console.WriteLine("The application runs in the system tray. Right-click the tray icon");
        Console.WriteLine("to access settings and controls.");
    }

    private static void CheckActivity(object? sender, EventArgs e)
    {
        try
        {
            if (appSettings == null || IsPaused) return;
            
            bool activityDetected = false;
            string activityType = "";
            uint currentTime = 0;
            
            // Get current time safely
            try
            {
                currentTime = GetTickCount();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get current time", ex);
                return; // Can't continue without current time
            }
            
            // Check keyboard/mouse activity
            try
            {
                uint currentInputTime = GetLastInputTime();
                if (currentInputTime != 0 && currentInputTime != lastInputTime)
                {
                    lastInputTime = currentInputTime;
                    lastActivityTime = currentTime;
                    activityDetected = true;
                    activityType = "Keyboard/Mouse";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check keyboard/mouse activity", ex);
                // Continue with controller check even if input check fails
            }
            
            // Check controller activity
            try
            {
                for (uint i = 0; i < 4; i++)
                {
                    try
                    {
                        XINPUT_STATE state = new XINPUT_STATE();
                        uint result = XInputGetState(i, ref state);
                        
                        if (result == 0) // Controller connected
                        {
                            if (state.dwPacketNumber != lastControllerPackets[i])
                            {
                                lastControllerPackets[i] = state.dwPacketNumber;
                                lastActivityTime = currentTime;
                                activityDetected = true;
                                activityType = $"Controller {i + 1}";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to check controller {i + 1} activity", ex);
                        // Continue checking other controllers
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check controller activity", ex);
                // Continue with timeout check
            }
            
            // Calculate idle time since last activity
            uint idleTimeMs = lastActivityTime > 0 ? currentTime - lastActivityTime : 0;
            uint idleTimeSeconds = idleTimeMs / 1000;
            uint timeoutSeconds = (uint)(appSettings.TimeoutMinutes * 60);
            
            // Verbose logging - show status every 5 seconds
            if (verboseLogging)
            {
                try
                {
                    if (activityDetected)
                    {
                        Logger.Info($"{activityType} activity detected");
                    }
                    else
                    {
                        Logger.Info($"No activity detected since {idleTimeSeconds} seconds (timeout: {timeoutSeconds}s)");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to log activity status", ex);
                }
            }
            
            // Check if we've exceeded the timeout (only if no activity)
            if (!activityDetected && idleTimeSeconds >= timeoutSeconds)
            {
                try
                {
                    Logger.Info($"TIMEOUT REACHED! Triggering {appSettings.Mode}");
                    
                    if (appSettings.Mode == "Sleep")
                    {
                        Logger.Info("Forcing system sleep");
                        if (ForceSleep(hibernate: false, force: appSettings.GuaranteedSleep))
                        {
                            Logger.Info("Sleep command sent successfully");
                        }
                        else
                        {
                            Logger.Error("Failed to send sleep command");
                        }
                    }
                    else if (appSettings.Mode == "Shutdown")
                    {
                        Logger.Info("Forcing system shutdown");
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "shutdown.exe",
                                Arguments = "/s /t 0",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            });
                            Logger.Info("Shutdown command sent successfully");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to send shutdown command", ex);
                        }
                    }
                    
                    // Reset activity timer to prevent immediate re-triggering
                    lastActivityTime = currentTime;
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to handle timeout action", ex);
                    // Reset timer to prevent continuous failures
                    lastActivityTime = currentTime;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Critical error in activity monitoring timer", ex);
            // Try to reset activity time to prevent getting stuck
            try
            {
                lastActivityTime = GetTickCount();
            }
            catch
            {
                // If we can't even get the current time, just continue
                // The timer will try again on the next interval
            }
        }
    }

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            // Parse command line arguments
            ParseCommandLineArgs(args);
            
            Logger.Info("IdleForce Tray starting");
            if (verboseLogging)
                Logger.Info("Verbose activity logging enabled");
            
            // Single instance check using named mutex
            using (var mutex = new Mutex(true, @"Global\IdleForceTray", out bool createdNew))
            {
                if (!createdNew)
                {
                    Logger.Info("Another instance is already running, exiting");
                    return;
                }

                Logger.Debug("Single instance check passed");

                // Load settings at startup
                appSettings = SettingsManager.LoadSettings();
                Logger.Info("Settings loaded successfully");

                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
                Logger.Debug("Application configuration initialized");

                // Initialize activity monitoring
                Logger.Debug("Initializing activity monitoring");
                lastInputTime = GetLastInputTime();
                lastActivityTime = GetTickCount();
                
                // Initialize controller packet numbers
                for (uint i = 0; i < 4; i++)
                {
                    XINPUT_STATE state = new XINPUT_STATE();
                    if (XInputGetState(i, ref state) == 0)
                    {
                        lastControllerPackets[i] = state.dwPacketNumber;
                        Logger.Info($"Controller {i + 1} detected");
                    }
                }
                
                // Start activity monitoring timer
                activityTimer = new System.Windows.Forms.Timer();
                activityTimer.Interval = appSettings.CheckIntervalSeconds * 1000; // Convert to milliseconds
                activityTimer.Tick += CheckActivity;
                activityTimer.Start();
                Logger.Info($"Activity monitoring timer started - checking every {appSettings.CheckIntervalSeconds} seconds");
                Logger.Info($"Timeout: {appSettings.TimeoutMinutes} minutes, Mode: {appSettings.Mode}");
                
                Logger.Debug("Starting main application loop with Form1");
                try
                {
                    mainForm = new Form1(appSettings);
                    Application.Run(mainForm);
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to start Windows Forms application", ex);
                    Logger.Info("Application will continue with background monitoring only");
                    
                    // Keep the application alive with manual timer calls
                    while (true)
                    {
                        Thread.Sleep(appSettings.CheckIntervalSeconds * 1000);
                        CheckActivity(null, EventArgs.Empty);
                    }
                }
                
                Logger.Debug("Application loop ended");
                
                // Clean up
                activityTimer?.Stop();
                activityTimer?.Dispose();
                Logger.Debug("Cleanup completed");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal error", ex);
        }
    }    
}