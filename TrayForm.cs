namespace IdleForceTray;

public partial class TrayForm : Form
{
    private NotifyIcon trayIcon = null!;
    private ContextMenuStrip contextMenu = null!;
    private ToolStripMenuItem statusLabel = null!;
    private ToolStripMenuItem modeSubmenu = null!;
    private ToolStripMenuItem timeoutSubmenu = null!;
    private ToolStripMenuItem pauseResumeItem = null!;
    private ToolStripMenuItem sleepNowItem = null!;
    private ToolStripMenuItem shutdownNowItem = null!;
    private ToolStripMenuItem startupItem = null!;
    private ToolStripMenuItem viewLogsItem = null!;
    private ToolStripMenuItem exitItem = null!;
    private System.Windows.Forms.Timer updateTimer = null!;

    // Application state
    private bool isPaused = false;
    private Settings appSettings;
    
    public bool IsPaused => isPaused;

    public TrayForm(Settings settings)
    {
        appSettings = settings;
        InitializeComponent();
        
        // Sync startup setting with actual system state
        bool actualStartupEnabled = Program.IsStartupShortcutEnabled();
        if (actualStartupEnabled != appSettings.StartWithWindows)
        {
            appSettings.StartWithWindows = actualStartupEnabled;
            SettingsManager.SaveSettings(appSettings);
        }
        
        InitializeTrayIcon();
        
        // Hide the form immediately since this is a tray-only app
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.Visible = false;
        
        // Initialize update timer for live countdown
        updateTimer = new System.Windows.Forms.Timer();
        updateTimer.Interval = 1000; // Update every second
        updateTimer.Tick += UpdateTimer_Tick;
        updateTimer.Start();
    }

    private Icon LoadEmbeddedIcon()
    {
        try
        {
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("IdleForceTray.IdleForceTray_icons.IdleForceTray.ico"))
            {
                if (stream != null)
                {
                    return new Icon(stream);
                }
            }
        }
        catch (Exception)
        {
            // Fallback to system icon if loading fails
        }
        
        return SystemIcons.Application;
    }

    private void InitializeTrayIcon()
    {
        // Create the NotifyIcon
        trayIcon = new NotifyIcon();
        trayIcon.Icon = LoadEmbeddedIcon();
        trayIcon.Visible = true;
        UpdateTooltip();

        // Create context menu
        CreateContextMenu();
        trayIcon.ContextMenuStrip = contextMenu;

        // Handle tray icon events
        trayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;
    }

    private void CreateContextMenu()
    {
        contextMenu = new ContextMenuStrip();

        // Status label (non-interactive)
        statusLabel = new ToolStripMenuItem();
        statusLabel.Enabled = false;
        UpdateStatusLabel();
        contextMenu.Items.Add(statusLabel);

        contextMenu.Items.Add(new ToolStripSeparator());

        // Mode submenu
        modeSubmenu = new ToolStripMenuItem("Mode");
        var sleepMode = new ToolStripMenuItem("Sleep", null, ModeItem_Click) { Tag = "Sleep", Checked = appSettings.Mode == "Sleep" };
        var shutdownMode = new ToolStripMenuItem("Shutdown", null, ModeItem_Click) { Tag = "Shutdown", Checked = appSettings.Mode == "Shutdown" };
        modeSubmenu.DropDownItems.AddRange(new[] { sleepMode, shutdownMode });
        contextMenu.Items.Add(modeSubmenu);

        // Timeout submenu
        timeoutSubmenu = new ToolStripMenuItem("Timeout");
        var timeouts = new[] { 1, 2, 5, 10, 15, 20, 30, 45 };
        foreach (var timeout in timeouts)
        {
            var timeoutItem = new ToolStripMenuItem($"{timeout}m", null, TimeoutItem_Click) 
            { 
                Tag = timeout, 
                Checked = timeout == appSettings.TimeoutMinutes 
            };
            timeoutSubmenu.DropDownItems.Add(timeoutItem);
        }
        contextMenu.Items.Add(timeoutSubmenu);

        contextMenu.Items.Add(new ToolStripSeparator());

        // Pause/Resume
        pauseResumeItem = new ToolStripMenuItem(isPaused ? "Resume" : "Pause", null, PauseResume_Click);
        contextMenu.Items.Add(pauseResumeItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // Sleep now
        sleepNowItem = new ToolStripMenuItem("Sleep now", null, SleepNow_Click);
        contextMenu.Items.Add(sleepNowItem);

        // Shutdown now
        shutdownNowItem = new ToolStripMenuItem("Shutdown now", null, ShutdownNow_Click);
        contextMenu.Items.Add(shutdownNowItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // Start with Windows toggle
        startupItem = new ToolStripMenuItem("Start with Windows", null, Startup_Click) 
        { 
            Checked = appSettings.StartWithWindows 
        };
        contextMenu.Items.Add(startupItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // View Logs
        viewLogsItem = new ToolStripMenuItem("View Logs", null, ViewLogs_Click);
        contextMenu.Items.Add(viewLogsItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // Exit
        exitItem = new ToolStripMenuItem("Exit", null, Exit_Click);
        contextMenu.Items.Add(exitItem);
    }

    private void UpdateStatusLabel()
    {
        if (isPaused)
        {
            statusLabel.Text = "Status: Paused";
        }
        else
        {
            var timeLeft = GetTimeUntilAction();
            statusLabel.Text = $"Status: {appSettings.Mode} in {FormatTime(timeLeft)}";
        }
    }
    
    private TimeSpan GetTimeUntilAction()
    {
        try
        {
            uint currentTime = Program.GetCurrentTickCount();
            uint lastActivity = Program.GetLastActivityTime();
            uint timeoutMs = (uint)(appSettings.TimeoutMinutes * 60 * 1000);
            
            if (lastActivity == 0)
            {
                return TimeSpan.FromMinutes(appSettings.TimeoutMinutes);
            }
            
            uint idleTime = currentTime - lastActivity;
            if (idleTime >= timeoutMs)
            {
                return TimeSpan.Zero;
            }
            
            uint remainingMs = timeoutMs - idleTime;
            return TimeSpan.FromMilliseconds(remainingMs);
        }
        catch
        {
            return TimeSpan.FromMinutes(appSettings.TimeoutMinutes);
        }
    }
    
    private string FormatTime(TimeSpan timeSpan)
    {
        if (timeSpan.TotalSeconds < 60)
        {
            return $"{(int)Math.Ceiling(timeSpan.TotalSeconds)}s";
        }
        else
        {
            return $"{(int)Math.Ceiling(timeSpan.TotalMinutes)}m";
        }
    }

    private void UpdateTooltip()
    {
        if (isPaused)
        {
            trayIcon.Text = "IdleForce: paused";
        }
        else
        {
            trayIcon.Text = $"IdleForce: running, {appSettings.Mode} in {appSettings.TimeoutMinutes}m";
        }
    }

    private void UpdateMenuChecks()
    {
        // Update mode checks
        foreach (ToolStripMenuItem item in modeSubmenu.DropDownItems)
        {
            item.Checked = item.Tag?.ToString() == appSettings.Mode;
        }

        // Update timeout checks
        foreach (ToolStripMenuItem item in timeoutSubmenu.DropDownItems)
        {
            item.Checked = item.Tag is int timeout && timeout == appSettings.TimeoutMinutes;
        }

        // Update other toggles
        pauseResumeItem.Text = isPaused ? "Resume" : "Pause";
        startupItem.Checked = appSettings.StartWithWindows;
    }

    #region Event Handlers

    private void TrayIcon_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        // Double-click to toggle pause/resume
        PauseResume_Click(sender, e);
    }

    private void ModeItem_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item && item.Tag is string mode)
        {
            appSettings.Mode = mode;
            SettingsManager.SaveSettings(appSettings);
            UpdateStatusLabel();
            UpdateTooltip();
            UpdateMenuChecks();
        }
    }

    private void TimeoutItem_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item && item.Tag is int timeout)
        {
            appSettings.TimeoutMinutes = timeout;
            SettingsManager.SaveSettings(appSettings);
            UpdateStatusLabel();
            UpdateTooltip();
            UpdateMenuChecks();
        }
    }

    private void PauseResume_Click(object? sender, EventArgs e)
    {
        isPaused = !isPaused;
        UpdateStatusLabel();
        UpdateTooltip();
        UpdateMenuChecks();
    }

    private void SleepNow_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Sleep the system now?", "IdleForce", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Program.ForceSleep(hibernate: false, force: true);
        }
    }

    private void ShutdownNow_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Shutdown the system now?", "IdleForce", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/s /t 0",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initiate shutdown: {ex.Message}", "IdleForce Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }


    private void Startup_Click(object? sender, EventArgs e)
    {
        bool newState = !appSettings.StartWithWindows;
        
        // Try to apply the startup setting
        if (Program.SetStartupEnabled(newState))
        {
            appSettings.StartWithWindows = newState;
            SettingsManager.SaveSettings(appSettings);
            MessageBox.Show($"Start with Windows: {(newState ? "Enabled" : "Disabled")}", "IdleForce", 
                           MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Failed to update startup setting. Please check permissions and try again.", "IdleForce Error", 
                           MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        
        UpdateMenuChecks();
    }

    private void ViewLogs_Click(object? sender, EventArgs e)
    {
        try
        {
            // Get the log file path from the Logger class
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "IdleForce", "logs");
            string logFilePath = Path.Combine(logDirectory, "IdleForce.log");
            
            // Ensure the log directory and file exist
            if (!Directory.Exists(logDirectory))
            {
                MessageBox.Show("Log directory not found. No logs have been created yet.", "IdleForce", 
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            if (!File.Exists(logFilePath))
            {
                MessageBox.Show("Log file not found. No logs have been created yet.", "IdleForce", 
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // Launch PowerShell with Get-Content -Wait to tail the log file
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoExit -Command \"Get-Content -Path '{logFilePath}' -Wait -Tail 50\"",
                UseShellExecute = true,
                CreateNoWindow = false
            };
            
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open log viewer: {ex.Message}", "IdleForce Error", 
                           MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Exit_Click(object? sender, EventArgs e)
    {
        Application.Exit();
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateStatusLabel();
        UpdateTooltip();
    }

    #endregion

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Hide to tray instead of closing
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
        }
        else
        {
            // Clean up resources when actually exiting
            updateTimer?.Stop();
            updateTimer?.Dispose();
            trayIcon?.Dispose();
            base.OnFormClosing(e);
        }
    }

    protected override void SetVisibleCore(bool value)
    {
        // Keep the form hidden
        base.SetVisibleCore(false);
    }
}
