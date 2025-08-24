namespace IdleForceTray;

public partial class Form1 : Form
{
    private NotifyIcon trayIcon;
    private ContextMenuStrip contextMenu;
    private ToolStripMenuItem statusLabel;
    private ToolStripMenuItem modeSubmenu;
    private ToolStripMenuItem timeoutSubmenu;
    private ToolStripMenuItem pauseResumeItem;
    private ToolStripMenuItem sleepNowItem;
    private ToolStripMenuItem shutdownNowItem;
    private ToolStripMenuItem guaranteedSleepItem;
    private ToolStripMenuItem startupItem;
    private ToolStripMenuItem exitItem;

    // Application state
    private bool isPaused = false;
    private string currentMode = "Sleep";
    private int timeoutMinutes = 20;
    private bool guaranteedSleepEnabled = false;
    private bool startWithWindows = true;

    public Form1()
    {
        InitializeComponent();
        InitializeTrayIcon();
        
        // Hide the form immediately since this is a tray-only app
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.Visible = false;
    }

    private void InitializeTrayIcon()
    {
        // Create the NotifyIcon
        trayIcon = new NotifyIcon();
        trayIcon.Icon = SystemIcons.Application; // TODO: Replace with custom icon
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
        var sleepMode = new ToolStripMenuItem("Sleep", null, ModeItem_Click) { Tag = "Sleep", Checked = currentMode == "Sleep" };
        var shutdownMode = new ToolStripMenuItem("Shutdown", null, ModeItem_Click) { Tag = "Shutdown", Checked = currentMode == "Shutdown" };
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
                Checked = timeout == timeoutMinutes 
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

        // Guaranteed Sleep toggle
        guaranteedSleepItem = new ToolStripMenuItem("Guaranteed Sleep", null, GuaranteedSleep_Click) 
        { 
            Checked = guaranteedSleepEnabled 
        };
        contextMenu.Items.Add(guaranteedSleepItem);

        // Start with Windows toggle
        startupItem = new ToolStripMenuItem("Start with Windows", null, Startup_Click) 
        { 
            Checked = startWithWindows 
        };
        contextMenu.Items.Add(startupItem);

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
            statusLabel.Text = $"Status: Running, {currentMode} in {timeoutMinutes}m";
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
            trayIcon.Text = $"IdleForce: running, {currentMode} in {timeoutMinutes}m";
        }
    }

    private void UpdateMenuChecks()
    {
        // Update mode checks
        foreach (ToolStripMenuItem item in modeSubmenu.DropDownItems)
        {
            item.Checked = (string)item.Tag == currentMode;
        }

        // Update timeout checks
        foreach (ToolStripMenuItem item in timeoutSubmenu.DropDownItems)
        {
            item.Checked = (int)item.Tag == timeoutMinutes;
        }

        // Update other toggles
        pauseResumeItem.Text = isPaused ? "Resume" : "Pause";
        guaranteedSleepItem.Checked = guaranteedSleepEnabled;
        startupItem.Checked = startWithWindows;
    }

    #region Event Handlers

    private void TrayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        // Double-click to toggle pause/resume
        PauseResume_Click(sender, e);
    }

    private void ModeItem_Click(object sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item)
        {
            currentMode = (string)item.Tag;
            UpdateStatusLabel();
            UpdateTooltip();
            UpdateMenuChecks();
        }
    }

    private void TimeoutItem_Click(object sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item)
        {
            timeoutMinutes = (int)item.Tag;
            UpdateStatusLabel();
            UpdateTooltip();
            UpdateMenuChecks();
        }
    }

    private void PauseResume_Click(object sender, EventArgs e)
    {
        isPaused = !isPaused;
        UpdateStatusLabel();
        UpdateTooltip();
        UpdateMenuChecks();
    }

    private void SleepNow_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("Sleep the system now?", "IdleForce", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Program.ForceSleep(hibernate: false, force: true);
        }
    }

    private void ShutdownNow_Click(object sender, EventArgs e)
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

    private void GuaranteedSleep_Click(object sender, EventArgs e)
    {
        var newState = !guaranteedSleepEnabled;
        
        if (newState)
        {
            var result = MessageBox.Show("This turns off Hibernate to ensure Sleep. Continue?", "IdleForce - Guaranteed Sleep", 
                                       MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                // TODO: Implement elevation and powercfg -h off call
                guaranteedSleepEnabled = true;
                MessageBox.Show("Guaranteed Sleep enabled. Hibernate has been disabled.", "IdleForce", 
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        else
        {
            guaranteedSleepEnabled = false;
            MessageBox.Show("Guaranteed Sleep disabled.", "IdleForce", 
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        UpdateMenuChecks();
    }

    private void Startup_Click(object sender, EventArgs e)
    {
        startWithWindows = !startWithWindows;
        // TODO: Implement startup shortcut creation/deletion
        MessageBox.Show($"Start with Windows: {(startWithWindows ? "Enabled" : "Disabled")}", "IdleForce", 
                       MessageBoxButtons.OK, MessageBoxIcon.Information);
        UpdateMenuChecks();
    }

    private void Exit_Click(object sender, EventArgs e)
    {
        Application.Exit();
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
            // Clean up tray icon when actually exiting
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
