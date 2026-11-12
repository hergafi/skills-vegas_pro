using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ScriptPortal.Vegas;

public class EntryPoint
{
    public void FromVegas(Vegas vegas)
    {
        using (SmartBatchStabilizationDialog dialog = new SmartBatchStabilizationDialog(vegas))
        {
            dialog.ShowDialog(vegas.MainWindow);
        }
    }
}

public class SmartBatchStabilizationDialog : Form
{
    private Vegas vegas;

    private ComboBox ApplyAsBox;
    private ComboBox ScopeBox;
    private ComboBox PresetBox;
    private ComboBox ModeBox;
    private TrackBar SmoothingTrackBar;
    private NumericUpDown SmoothingAmount;
    private CheckBox AnalyzeBox;
    private CheckBox SkipExistingBox;

    private TextBox PresetNameBox;
    private ComboBox PresetListBox;
    private Label InfoLabel;
    private Label StatusLabel;
    private TextBox LogBox;

    private string lastError = "";
    private readonly List<string> parameterErrors = new List<string>();
    private readonly StringBuilder log = new StringBuilder();
    private bool loadingPreset;

    private static readonly string PresetFile =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SmartStabilization", "presets.txt");

    public SmartBatchStabilizationDialog(Vegas vegas)
    {
        this.vegas = vegas;

        Text = "Smart Batch Stabilization";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(430, 592);
        BackColor = Color.FromArgb(30, 30, 34);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9f);

        Icon = CreateScriptIcon();

        int y = 12;
        int labelLeft = 15;
        int controlLeft = 130;
        int controlWidth = 285;

        AddLabel("Apply as:", labelLeft, y);
        ApplyAsBox = new ComboBox();
        ApplyAsBox.Left = controlLeft; ApplyAsBox.Top = y; ApplyAsBox.Width = controlWidth;
        ApplyAsBox.DropDownStyle = ComboBoxStyle.DropDownList;
        ApplyAsBox.Items.AddRange(new object[] {
            "Event FX (segment only via subclip)",
            "Media FX (whole media)"
        });
        ApplyAsBox.SelectedIndex = 0;
        Controls.Add(ApplyAsBox);
        y += 32;

        AddLabel("Apply to:", labelLeft, y);
        ScopeBox = new ComboBox();
        ScopeBox.Left = controlLeft; ScopeBox.Top = y; ScopeBox.Width = controlWidth;
        ScopeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        ScopeBox.Items.AddRange(new object[] { "Selected only", "All video events" });
        ScopeBox.SelectedIndex = 0;
        Controls.Add(ScopeBox);
        y += 32;

        AddLabel("Preset:", labelLeft, y);
        PresetBox = new ComboBox();
        PresetBox.Left = controlLeft; PresetBox.Top = y; PresetBox.Width = controlWidth;
        PresetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        PresetBox.Items.AddRange(new object[] { "(Default)", "full", "1.4", "7.0" });
        PresetBox.SelectedIndex = 0;
        Controls.Add(PresetBox);
        y += 32;

        AddLabel("Mode:", labelLeft, y);
        ModeBox = new ComboBox();
        ModeBox.Left = controlLeft; ModeBox.Top = y; ModeBox.Width = controlWidth;
        ModeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        ModeBox.Items.AddRange(new object[] { "Smooth motion", "Freeze motion" });
        ModeBox.SelectedIndex = 0;
        Controls.Add(ModeBox);
        y += 32;

        AddLabel("Smoothing:", labelLeft, y);
        SmoothingAmount = new NumericUpDown();
        SmoothingAmount.Left = controlLeft; SmoothingAmount.Top = y; SmoothingAmount.Width = 75;
        SmoothingAmount.Minimum = 0; SmoothingAmount.Maximum = 10;
        SmoothingAmount.DecimalPlaces = 1; SmoothingAmount.Increment = 0.1m;
        SmoothingAmount.Value = 3.0m;
        SmoothingAmount.ValueChanged += new EventHandler(SmoothingAmount_ValueChanged);
        Controls.Add(SmoothingAmount);
        y += 26;

        SmoothingTrackBar = new TrackBar();
        SmoothingTrackBar.Left = labelLeft; SmoothingTrackBar.Top = y;
        SmoothingTrackBar.Width = 400; SmoothingTrackBar.Height = 24;
        SmoothingTrackBar.Minimum = 0; SmoothingTrackBar.Maximum = 100;
        SmoothingTrackBar.Value = 30; SmoothingTrackBar.TickFrequency = 10;
        SmoothingTrackBar.SmallChange = 1; SmoothingTrackBar.LargeChange = 10;
        SmoothingTrackBar.ValueChanged += new EventHandler(SmoothingTrackBar_ValueChanged);
        SmoothingTrackBar.BackColor = Color.FromArgb(30, 30, 34);
        Controls.Add(SmoothingTrackBar);
        y += 28;

        SkipExistingBox = new CheckBox();
        SkipExistingBox.Left = labelLeft; SkipExistingBox.Top = y; SkipExistingBox.Width = 190;
        SkipExistingBox.Text = "Skip existing";
        SkipExistingBox.Checked = true;
        SkipExistingBox.ForeColor = Color.Gainsboro;
        Controls.Add(SkipExistingBox);

        AnalyzeBox = new CheckBox();
        AnalyzeBox.Left = 210; AnalyzeBox.Top = y; AnalyzeBox.Width = 205;
        AnalyzeBox.Text = "Trigger analysis";
        AnalyzeBox.Checked = true;
        AnalyzeBox.ForeColor = Color.Gainsboro;
        Controls.Add(AnalyzeBox);
        y += 32;

        AddLabel("Preset:", labelLeft, y);
        PresetNameBox = new TextBox();
        PresetNameBox.Left = controlLeft; PresetNameBox.Top = y; PresetNameBox.Width = 150;
        PresetNameBox.Text = "My stabilization preset";
        PresetNameBox.BackColor = Color.FromArgb(24, 24, 29);
        PresetNameBox.ForeColor = Color.Gainsboro;
        Controls.Add(PresetNameBox);

        Button savePresetButton = MakeButton("Save preset name", 285, y, 130, Color.FromArgb(55, 55, 64));
        savePresetButton.Click += new EventHandler(SavePresetButton_Click);
        Controls.Add(savePresetButton);
        y += 32;

        AddLabel("Preset list:", labelLeft, y);
        PresetListBox = new ComboBox();
        PresetListBox.Left = controlLeft; PresetListBox.Top = y; PresetListBox.Width = 150;
        PresetListBox.DropDownStyle = ComboBoxStyle.DropDownList;
        PresetListBox.SelectedIndexChanged += new EventHandler(PresetListBox_SelectedIndexChanged);
        Controls.Add(PresetListBox);

        Button deletePresetButton = MakeButton("Delete preset", 285, y, 130, Color.FromArgb(90, 55, 55));
        deletePresetButton.Click += new EventHandler(DeletePresetButton_Click);
        Controls.Add(deletePresetButton);
        y += 32;

        InfoLabel = new Label();
        InfoLabel.Left = labelLeft; InfoLabel.Top = y; InfoLabel.Width = 400; InfoLabel.Height = 36;
        InfoLabel.BorderStyle = BorderStyle.FixedSingle;
        InfoLabel.Padding = new Padding(6);
        InfoLabel.Text = "Apply / Apply Preset = add + setup + analysis. Apply Forced = add another stabilization alongside an existing one. Event FX uses subclip+AddTake (segment only).";
        InfoLabel.ForeColor = Color.Gainsboro;
        Controls.Add(InfoLabel);
        y += 40;

        StatusLabel = new Label();
        StatusLabel.Left = labelLeft; StatusLabel.Top = y; StatusLabel.Width = 400; StatusLabel.Height = 20;
        StatusLabel.ForeColor = Color.SkyBlue;
        Controls.Add(StatusLabel);
        y += 24;

        LogBox = new TextBox();
        LogBox.Left = labelLeft; LogBox.Top = y; LogBox.Width = 400; LogBox.Height = 70;
        LogBox.Multiline = true;
        LogBox.ReadOnly = true;
        LogBox.ScrollBars = ScrollBars.Vertical;
        LogBox.BackColor = Color.FromArgb(18, 18, 22);
        LogBox.ForeColor = Color.FromArgb(170, 220, 255);
        Controls.Add(LogBox);

        int rowY = LogBox.Bottom + 8;

        Button applyButton = MakeButton("Apply", 15, rowY, 85, Color.FromArgb(10, 132, 255));
        applyButton.Click += new EventHandler(ApplyButton_Click);
        Controls.Add(applyButton);

        Button applyPresetButton = MakeButton("Apply Preset", 106, rowY, 100, Color.FromArgb(55, 75, 120));
        applyPresetButton.Click += new EventHandler(ApplyPresetButton_Click);
        Controls.Add(applyPresetButton);

        Button applyForcedButton = MakeButton("Apply Forced", 212, rowY, 105, Color.FromArgb(200, 130, 30));
        applyForcedButton.Click += new EventHandler(ApplyForcedButton_Click);
        Controls.Add(applyForcedButton);

        Button resetButton = MakeButton("RESET", 323, rowY, 90, Color.FromArgb(165, 66, 66));
        resetButton.Click += new EventHandler(ResetButton_Click);
        Controls.Add(resetButton);

        int row2Y = rowY + 44;

        Button saveLogButton = MakeButton("Save log.txt...", 15, row2Y, 125, Color.FromArgb(55, 55, 64));
        saveLogButton.Click += new EventHandler(SaveLogButton_Click);
        Controls.Add(saveLogButton);

        Button exitButton = MakeButton("Exit", 147, row2Y, 130, Color.FromArgb(55, 55, 64));
        exitButton.DialogResult = DialogResult.OK;
        Controls.Add(exitButton);
        AcceptButton = exitButton;
        CancelButton = exitButton;

        LoadPresets();
    }

    private Icon CreateScriptIcon()
    {
        using (Bitmap bmp = new Bitmap(32, 32))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(30, 30, 34));
                using (Pen wave = new Pen(Color.FromArgb(0, 200, 255), 2.2f))
                {
                    Point[] pts = new Point[]
                    {
                        new Point(3, 16), new Point(8, 16), new Point(11, 7), new Point(14, 25),
                        new Point(17, 16), new Point(22, 16), new Point(25, 10), new Point(29, 16)
                    };
                    g.DrawLines(wave, pts);
                }
                using (SolidBrush dot = new SolidBrush(Color.FromArgb(255, 80, 80)))
                {
                    g.FillEllipse(dot, 23, 11, 6, 6);
                }
            }
            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S))
        {
            SaveProjectSafely();
            StatusLabel.Text = "Project saved. Ctrl+S pressed.";
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void AddLabel(string text, int left, int top)
    {
        Label label = new Label();
        label.Left = left; label.Top = top + 2; label.Width = 110; label.AutoSize = false;
        label.Text = text;
        label.ForeColor = Color.Gainsboro;
        Controls.Add(label);
    }

    private Button MakeButton(string text, int left, int top, int width, Color backColor)
    {
        Button b = new Button();
        b.Text = text;
        b.Left = left; b.Top = top; b.Width = width; b.Height = 34;
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = backColor;
        b.ForeColor = Color.White;
        return b;
    }

    private void SmoothingAmount_ValueChanged(object sender, EventArgs e)
    {
        int trackValue = (int)(SmoothingAmount.Value * 10);
        if (SmoothingTrackBar.Value != trackValue) SmoothingTrackBar.Value = trackValue;
    }

    private void SmoothingTrackBar_ValueChanged(object sender, EventArgs e)
    {
        decimal amount = (decimal)SmoothingTrackBar.Value / 10m;
        if (SmoothingAmount.Value != amount) SmoothingAmount.Value = amount;
    }

    private void ApplyButton_Click(object sender, EventArgs e)
    {
        try
        {
            SaveProjectSafely();
            log.Clear();
            if (LogBox != null) LogBox.Clear();
            ApplyStabilization(false);
            SaveLogToDesktop();
        }
        catch (Exception ex)
        {
            Log("FATAL: " + ex.GetType().FullName + ": " + ex.Message);
            PlayFailed();
            WriteErrorFile("FATAL", ex);
            MessageBox.Show("Error: " + ex.Message + "\n\nFull details: " + ErrorPath(), "Smart Batch Stabilization");
        }
    }

    private void ApplyPresetButton_Click(object sender, EventArgs e)
    {
        try
        {
            SaveProjectSafely();
            log.Clear();
            if (LogBox != null) LogBox.Clear();
            ApplyStabilization(false);
            SaveLogToDesktop();
        }
        catch (Exception ex)
        {
            Log("FATAL PRESET: " + ex.GetType().FullName + ": " + ex.Message);
            PlayFailed();
            MessageBox.Show("Error: " + ex.Message, "Smart Batch Stabilization");
        }
    }

    private void ApplyForcedButton_Click(object sender, EventArgs e)
    {
        try
        {
            SaveProjectSafely();
            log.Clear();
            if (LogBox != null) LogBox.Clear();
            ApplyStabilization(true);
            SaveLogToDesktop();
        }
        catch (Exception ex)
        {
            Log("FATAL FORCED: " + ex.GetType().FullName + ": " + ex.Message);
            PlayFailed();
            MessageBox.Show("Error: " + ex.Message, "Smart Batch Stabilization");
        }
    }

    private void ResetButton_Click(object sender, EventArgs e)
    {
        DialogResult confirm = MessageBox.Show(
            "WARNING:\n\n" +
            "1. The Video Stabilization analysis may still be running in the background.\n" +
            "   Removing the effect while it is being used can cause Vegas Pro to crash.\n\n" +
            "2. Only continue AFTER the analysis has completely finished for all events.\n\n" +
            "Do you want to continue with Reset?",
            "Smart Batch Stabilization - Reset",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.OK)
        {
            Log("RESET cancelled by user.");
            StatusLabel.Text = "Reset cancelled by user.";
            return;
        }

        try
        {
            SaveProjectSafely();
            log.Clear();
            if (LogBox != null) LogBox.Clear();
            ResetStabilization();
            SaveLogToDesktop();
        }
        catch (Exception ex)
        {
            Log("FATAL RESET: " + ex.GetType().FullName + ": " + ex.Message);
            PlayFailed();
            MessageBox.Show("Error: " + ex.Message, "Smart Batch Stabilization");
        }
    }

    private void SaveLogButton_Click(object sender, EventArgs e)
    {
        using (SaveFileDialog dlg = new SaveFileDialog())
        {
            dlg.Filter = "Text files (*.txt)|*.txt";
            dlg.FileName = "SmartBatchStabilization_log.txt";
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, log.ToString());
                    MessageBox.Show("Log saved to:\n" + dlg.FileName, "Smart Batch Stabilization");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not save log: " + ex.Message, "Smart Batch Stabilization");
                }
            }
        }
    }

    private void SavePresetButton_Click(object sender, EventArgs e)
    {
        string name = PresetNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Enter a preset name first.", "Smart Batch Stabilization");
            return;
        }
        if (name.Contains("|"))
        {
            MessageBox.Show("Preset name cannot contain '|'.", "Smart Batch Stabilization");
            return;
        }

        string line = name + "|" + ApplyAsBox.SelectedIndex + "|" + ScopeBox.SelectedIndex + "|" +
                      PresetBox.SelectedIndex + "|" + ModeBox.SelectedIndex + "|" +
                      ((double)SmoothingAmount.Value).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                      "|" + (SkipExistingBox.Checked ? "1" : "0") + "|" + (AnalyzeBox.Checked ? "1" : "0");

        List<string> lines = new List<string>();
        try
        {
            if (File.Exists(PresetFile)) lines.AddRange(File.ReadAllLines(PresetFile));
        }
        catch { }

        bool replaced = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith(name + "|", StringComparison.Ordinal))
            {
                lines[i] = line;
                replaced = true;
                break;
            }
        }
        if (!replaced) lines.Add(line);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PresetFile));
            File.WriteAllLines(PresetFile, lines.ToArray());
            LoadPresets();
            Log("Preset saved: " + name);
            StatusLabel.Text = "Preset saved: " + name;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not save preset: " + ex.Message, "Smart Batch Stabilization");
        }
    }

    private static readonly string SoundSuccessPath =
        @"C:\Program Files\BorisFX\Vegas Pro 2026\Script Menu\Sounds\success.mp3";
    private static readonly string SoundFailedPath =
        @"C:\Program Files\BorisFX\Vegas Pro 2026\Script Menu\Sounds\failed.mp3";

    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int mciSendString(string command, System.Text.StringBuilder ret, int retSize, IntPtr hwndCallback);

    private void PlaySoundFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            string alias = "sbsfx_" + Guid.NewGuid().ToString("N");
            mciSendString("open \"" + path + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero);
            mciSendString("play " + alias, null, 0, IntPtr.Zero);
        }
        catch { }
    }

    private void PlaySuccess()
    {
        PlaySoundFile(SoundSuccessPath);
    }

    private void PlayFailed()
    {
        PlaySoundFile(SoundFailedPath);
    }

    private void SaveProjectSafely()
    {
        try
        {
            if (!string.IsNullOrEmpty(vegas.Project.FilePath) && vegas.Project.IsModified)
            {
                Log("Auto-saving project...");
                vegas.SaveProject();
                Log("Project saved.");
            }
        }
        catch (Exception ex)
        {
            Log("Auto-save skipped: " + ex.Message);
        }
    }

    private void DeletePresetButton_Click(object sender, EventArgs e)
    {
        string selected = PresetListBox.SelectedItem == null ? null : PresetListBox.SelectedItem.ToString();
        if (string.IsNullOrEmpty(selected))
        {
            Log("Delete preset: no preset selected.");
            StatusLabel.Text = "Select a preset to delete.";
            return;
        }

        List<string> kept = new List<string>();
        try
        {
            if (File.Exists(PresetFile))
            {
                foreach (string line in File.ReadAllLines(PresetFile))
                {
                    if (!line.StartsWith(selected + "|", StringComparison.Ordinal)) kept.Add(line);
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(PresetFile));
            File.WriteAllLines(PresetFile, kept.ToArray());
            LoadPresets();
            Log("Preset deleted: " + selected);
            StatusLabel.Text = "Preset deleted: " + selected;
        }
        catch (Exception ex)
        {
            Log("Delete preset error: " + ex.Message);
            MessageBox.Show("Could not delete preset: " + ex.Message, "Smart Batch Stabilization");
        }
    }

    private void PresetListBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (loadingPreset || PresetListBox.SelectedIndex < 0) return;
        string selected = PresetListBox.SelectedItem.ToString();
        string line = null;
        try
        {
            if (File.Exists(PresetFile))
            {
                foreach (string l in File.ReadAllLines(PresetFile))
                {
                    if (l.StartsWith(selected + "|", StringComparison.Ordinal))
                    {
                        line = l;
                        break;
                    }
                }
            }
        }
        catch { }
        if (line != null) ApplyPresetLine(line);
    }

    private void ApplyPresetLine(string line)
    {
        string[] p = line.Split('|');
        if (p.Length < 8) return;
        int applyAs, scope, preset, mode;
        double smoothing;
        if (!int.TryParse(p[1], out applyAs) || !int.TryParse(p[2], out scope) || !int.TryParse(p[3], out preset) ||
            !int.TryParse(p[4], out mode)) return;
        if (!double.TryParse(p[5], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out smoothing)) return;

        loadingPreset = true;
        ApplyAsBox.SelectedIndex = Clamp(applyAs, 0, ApplyAsBox.Items.Count - 1);
        ScopeBox.SelectedIndex = Clamp(scope, 0, ScopeBox.Items.Count - 1);
        PresetBox.SelectedIndex = Clamp(preset, 0, PresetBox.Items.Count - 1);
        ModeBox.SelectedIndex = Clamp(mode, 0, ModeBox.Items.Count - 1);
        SmoothingAmount.Value = (decimal)smoothing;
        SkipExistingBox.Checked = p[6] == "1";
        AnalyzeBox.Checked = p[7] == "1";
        loadingPreset = false;

        Log("Preset loaded: " + p[0]);
    }

    private void LoadPresets()
    {
        loadingPreset = true;
        PresetListBox.Items.Clear();
        try
        {
            if (File.Exists(PresetFile))
            {
                foreach (string line in File.ReadAllLines(PresetFile))
                {
                    string name = line.Split('|')[0].Trim();
                    if (!string.IsNullOrEmpty(name)) PresetListBox.Items.Add(name);
                }
            }
        }
        catch { }
        if (PresetListBox.Items.Count > 0 && PresetListBox.SelectedIndex < 0)
        {
            PresetListBox.SelectedIndex = 0;
        }
        loadingPreset = false;
    }

    private static int Clamp(int v, int min, int max)
    {
        return v < min ? min : (v > max ? max : v);
    }

    private void Log(string text)
    {
        log.AppendLine(text);
        if (LogBox != null)
        {
            LogBox.AppendText(text + Environment.NewLine);
            Application.DoEvents();
        }
    }

    private void SaveLogToDesktop()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartBatchStabilization_log.txt");
        try { File.WriteAllText(path, log.ToString()); } catch { }
    }

    private string ErrorPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Stabilization_error.txt");
    }

    private void WriteErrorFile(string fatal, Exception fatalEx)
    {
        string path = ErrorPath();
        List<string> lines = new List<string>();
        lines.Add("=== Smart Batch Stabilization error log ===");
        lines.Add(fatal + ": " + (fatalEx != null ? fatalEx.GetType().FullName + ": " + fatalEx.Message : ""));
        if (fatalEx != null) lines.Add(fatalEx.StackTrace ?? "");
        try { File.WriteAllLines(path, lines.ToArray()); } catch { }
    }

    private void ApplyStabilization(bool force)
    {
        lastError = "";
        parameterErrors.Clear();

        PlugInNode plugin = FindStabilizePlugin(vegas);
        if (plugin == null)
        {
            string diagnostic = WriteDiagnostic(vegas);
            Log("Stabilization plug-in not found. Diagnostic: " + diagnostic);
            MessageBox.Show("Vegas Video Stabilization plug-in not found by scripting.\n\nDiagnostic: " + diagnostic, "Smart Batch Stabilization");
            return;
        }

        int mode = ApplyAsBox.SelectedIndex;
        List<VideoEvent> events = CollectEvents(vegas, ScopeBox.SelectedIndex);
        if (events.Count == 0)
        {
            MessageBox.Show("No video events found.", "Smart Batch Stabilization");
            return;
        }

        List<VideoEvent> already = new List<VideoEvent>();
        foreach (VideoEvent ev in events)
        {
            if (HasStabilizationAny(ev, plugin)) already.Add(ev);
        }
        if (!force && already.Count > 0)
        {
            Log("Already stabilized: " + already.Count + " event(s). Press RESET first (or use Apply Forced).");
            MessageBox.Show(
                "Video Stabilization is already applied to " + already.Count + " selected event(s).\n\n" +
                "Press RESET first, then Apply again (or use Apply Forced to add another stabilization alongside).",
                "Smart Batch Stabilization");
            StatusLabel.Text = "Already applied to " + already.Count + " event(s). Press RESET first.";
            PlayFailed();
            return;
        }

        string modeName = mode == 0 ? "EVENT FX (subclip)" : "MEDIA FX (whole media)";
        if (force) modeName = modeName + " (FORCED)";
        Log("Mode: " + modeName + " | Events: " + events.Count);
        Log("Settings: preset=" + PresetBox.Text + " mode=" + ModeBox.Text + " smoothing=" + SmoothingAmount.Value +
            " analyze=" + (AnalyzeBox.Checked ? "on" : "off") + " skipExisting=" + (SkipExistingBox.Checked ? "on" : "off"));

        int added = 0, updated = 0, skipped = 0, failed = 0;
        bool alreadyApplied = false;

        for (int i = 0; i < events.Count; i++)
        {
            VideoEvent ev = events[i];
            Log("");
            Log("[" + (i + 1) + "/" + events.Count + "] Processing event...");

            try
            {
                if (ev.ActiveTake == null || ev.ActiveTake.Media == null || !ev.ActiveTake.Media.HasVideo())
                {
                    Log("  Skipped: no video media.");
                    skipped++;
                    continue;
                }

                if (HasStabilizationAny(ev, plugin))
                {
                    if (!force)
                    {
                        Log("  Already stabilized; skipping. Press RESET to redo (or use Apply Forced).");
                        skipped++;
                        alreadyApplied = true;
                        continue;
                    }
                    Log("  Already stabilized; FORCING another stabilization effect alongside the old one.");
                }

                if (mode == 0)
                {
                    if (ApplyEventFxViaSubclip(ev, plugin, ref added, ref failed, AnalyzeBox.Checked))
                    {
                        Log("  Done via subclip.");
                    }
                    else
                    {
                        Log("  Failed via subclip.");
                        failed++;
                    }
                    continue;
                }

                Effect effect = null;
                if (force)
                {
                    effect = new Effect(plugin);
                    ev.ActiveTake.Media.Effects.Add(effect);
                    Log("  FORCED: added another Media FX: Video Stabilization (alongside existing)");
                    added++;
                }
                else
                {
                    effect = GetExistingStabilization(ev, plugin, false);
                    if (effect == null)
                    {
                        effect = new Effect(plugin);
                        ev.ActiveTake.Media.Effects.Add(effect);
                        Log("  Added Media FX: Video Stabilization");
                        added++;
                    }
                    else
                    {
                        Log("  Updated existing Media FX.");
                        updated++;
                    }
                }

                ConfigureStabilizeEffect(effect, (double)SmoothingAmount.Value, ModeBox.SelectedIndex, PresetBox.SelectedIndex);

                if (AnalyzeBox.Checked)
                {
                    Log("  Triggering analysis...");
                    TriggerAnalyze(effect);
                }
            }
            catch (Exception ex)
            {
                failed++;
                lastError = ex.GetType().FullName + ": " + ex.Message;
                Log("  ERROR: " + lastError);
            }

            StatusLabel.Text = "Progress: " + (i + 1) + "/" + events.Count + "  Added:" + added + " Updated:" + updated + " Skipped:" + skipped + " Failed:" + failed;
            Application.DoEvents();
        }

        string summary = "Added: " + added + "  Updated: " + updated + "  Skipped: " + skipped + "  Failed: " + failed;
        Log("");
        Log("Summary: " + summary);
        Log("NOTE: Video Stabilization analysis is running for the effect(s). Wait for it to finish completely before pressing RESET.");
        StatusLabel.Text = "Done. Analysis is running — wait for it to finish, then use RESET only if needed.";

        if (failed > 0) PlayFailed(); else PlaySuccess();

        if (!force && alreadyApplied && added == 0 && failed == 0)
        {
            PlayFailed();
            MessageBox.Show("Video Stabilization is already applied to the selected events.\n\nPress RESET first, then Apply again (or use Apply Forced).", "Smart Batch Stabilization");
        }
    }

        private bool ApplyEventFxViaSubclip(VideoEvent ev, PlugInNode plugin, ref int added, ref int failed, bool triggerAnalysis)
    {
        try
        {
            Take srcTake = ev.ActiveTake;
            Media srcMedia = srcTake.Media;
            bool alreadySegment = false;
            try { if (srcMedia != null) alreadySegment = srcMedia.IsSubclip(); } catch { }

            if (alreadySegment)
            {
                Log("  Event already uses a subclip; applying stabilization to the subclip directly.");
                Effect subEffect = new Effect(plugin);
                srcMedia.Effects.Add(subEffect);
                Log("  Added Media FX to existing subclip: Video Stabilization");
                ConfigureStabilizeEffect(subEffect, (double)SmoothingAmount.Value, ModeBox.SelectedIndex, PresetBox.SelectedIndex);
                if (triggerAnalysis)
                {
                    Log("  Triggering analysis on existing subclip...");
                    TriggerAnalyze(subEffect);
                }
                added++;
                return true;
            }

            Timecode sourceIn = srcTake.Offset;
            Timecode length = ev.Length;
            string sourcePath = srcTake.MediaPath;

            if (string.IsNullOrEmpty(sourcePath))
            {
                Log("  No source path; cannot build subclip.");
                return false;
            }

            string displayName = Path.GetFileNameWithoutExtension(sourcePath) + "_stab";
            Log("  Creating subclip: " + displayName + " [" + sourceIn + " + " + length + "]");

            Subclip sub = new Subclip(vegas.Project, sourcePath, sourceIn, length, false, displayName);
            VideoStream stream = sub.GetVideoStreamByIndex(0);
            if (stream == null)
            {
                Log("  Subclip has no video stream.");
                return false;
            }

            Take newTake = ev.AddTake(stream, true, displayName);
            Log("  Subclip take created and made active.");

            Effect effect = new Effect(plugin);
            sub.Effects.Add(effect);
            Log("  Added Media FX to subclip: Video Stabilization");

            ConfigureStabilizeEffect(effect, (double)SmoothingAmount.Value, ModeBox.SelectedIndex, PresetBox.SelectedIndex);

            if (triggerAnalysis)
            {
                Log("  Triggering analysis on subclip...");
                TriggerAnalyze(effect);
            }

            added++;
            return true;
        }
        catch (Exception ex)
        {
            lastError = ex.GetType().FullName + ": " + ex.Message;
            Log("  Subclip error: " + lastError);
            return false;
        }
    }

    private void ResetStabilization()
    {
        PlugInNode plugin = FindStabilizePlugin(vegas);
        if (plugin == null)
        {
            MessageBox.Show("Video Stabilization plug-in not found.", "Smart Batch Stabilization");
            return;
        }

        List<VideoEvent> events = CollectEvents(vegas, ScopeBox.SelectedIndex);
        if (events.Count == 0)
        {
            MessageBox.Show("No video events found.", "Smart Batch Stabilization");
            return;
        }

        bool anyStabilization = false;
        foreach (VideoEvent ev in events)
        {
            if (HasStabilizationAny(ev, plugin)) { anyStabilization = true; break; }
        }
        if (!anyStabilization)
        {
            Log("RESET: no stabilization effects found in the selected events.");
            StatusLabel.Text = "No stabilization effects found in the selected events.";
            PlayFailed();
            MessageBox.Show("No Video Stabilization effects found in the selected videos.\n\nNothing to reset.", "Smart Batch Stabilization");
            return;
        }

        Log("RESET: removing Video Stabilization from " + events.Count + " event(s)...");
        Log("NOTE: If analysis is still running, wait for it to finish before pressing RESET.");
        int removed = 0;
        for (int i = 0; i < events.Count; i++)
        {
            VideoEvent ev = events[i];
            try
            {
                Log("  Event " + (i + 1) + "/" + events.Count + ":");

                int r = RemoveStabilization(ev, plugin);
                Log("    Removed " + r + " effect(s)");
                removed += r;
            }
            catch (Exception ex)
            {
                Log("    Event " + (i + 1) + "/" + events.Count + " ERROR: " + ex.GetType().FullName + ": " + ex.Message);
            }

            StatusLabel.Text = "RESET progress: " + (i + 1) + "/" + events.Count + "  Removed:" + removed;
            Application.DoEvents();
        }

        string summary = "Removed " + removed + " stabilization effect(s).";
        Log(summary);
        StatusLabel.Text = summary;

        if (removed > 0) PlaySuccess(); else PlayFailed();
    }

    private int RemoveStabilization(VideoEvent ev, PlugInNode plugin)
    {
        int removed = 0;
        RemoveFromEffects(ref removed, plugin, ev, true);
        RemoveFromEffects(ref removed, plugin, ev, false);
        return removed;
    }

    private void RemoveFromEffects(ref int removed, PlugInNode plugin, VideoEvent ev, bool eventFx)
    {
        string tag = eventFx ? "EventFX" : "MediaFX";

        while (true)
        {
            int index = -1;
            int i = 0;

            try
            {
                IEnumerable<Effect> effects = eventFx ? ev.Effects : (ev.ActiveTake != null && ev.ActiveTake.Media != null ? ev.ActiveTake.Media.Effects : null);
                if (effects == null) break;

                foreach (Effect effect in effects)
                {
                    try
                    {
                        if (effect != null && effect.PlugIn == plugin)
                        {
                            index = i;
                            break;
                        }
                    }
                    catch { }
                    i++;
                }
            }
            catch (Exception ex)
            {
                Log("    " + tag + " enumeration error: " + ex.Message);
                break;
            }

            if (index < 0) break;

            try
            {
                if (eventFx) ev.Effects.RemoveAt(index);
                else ev.ActiveTake.Media.Effects.RemoveAt(index);
                removed++;
                Log("    " + tag + " removed at index " + index);
            }
            catch (Exception ex)
            {
                Log("    " + tag + " removeAt error at index " + index + ": " + ex.Message);
                break;
            }

            Application.DoEvents();
        }
    }

    private PlugInNode FindStabilizePlugin(Vegas vegas)
    {
        PlugInNode[] roots = {
            TryGet(() => vegas.VideoFX),
            TryGet(() => vegas.VideoFXPackages),
            TryGet(() => vegas.VideoCompositeFX),
            TryGet(() => vegas.PlugIns)
        };

        foreach (PlugInNode root in roots)
        {
            PlugInNode found = FindInTree(root, NodeMatch.ExactUniqueID);
            if (found != null) return found;
        }
        foreach (PlugInNode root in roots)
        {
            PlugInNode found = FindInTree(root, NodeMatch.SonyOFX);
            if (found != null) return found;
        }
        foreach (PlugInNode root in roots)
        {
            PlugInNode found = FindInTree(root, NodeMatch.VegasName);
            if (found != null) return found;
        }
        foreach (PlugInNode root in roots)
        {
            PlugInNode found = FindInTree(root, NodeMatch.GenericVegasLike);
            if (found != null) return found;
        }
        return null;
    }

    private enum NodeMatch { ExactUniqueID, SonyOFX, VegasName, GenericVegasLike }

    private PlugInNode TryGet(System.Func<PlugInNode> getter)
    {
        try { return getter(); } catch { return null; }
    }

    private PlugInNode FindInTree(PlugInNode node, NodeMatch match)
    {
        if (node == null) return null;

        try { if (IsStabilizeNode(node, match)) return node; } catch { }

        bool isContainer = false;
        try { isContainer = node.IsContainer; } catch { return null; }
        if (!isContainer) return null;

        int count = 0;
        try { count = node.Count; } catch { return null; }

        for (int i = 0; i < count; i++)
        {
            PlugInNode child = null;
            try { child = node.GetChild(i); } catch { continue; }
            if (child == null) continue;
            PlugInNode found = FindInTree(child, match);
            if (found != null) return found;
        }
        return null;
    }

    private bool IsStabilizeNode(PlugInNode node, NodeMatch match)
    {
        bool isLeaf = false;
        try { isLeaf = !node.IsContainer; } catch { isLeaf = false; }
        if (!isLeaf) return false;

        string uid = "", name = "", group = "";
        try { uid = node.UniqueID ?? ""; } catch { }
        try { name = node.Name ?? ""; } catch { }
        try { group = node.Group ?? ""; } catch { }

        string haystack = (uid + " " + name + " " + group).ToLowerInvariant();
        bool looksLikeBcc = haystack.Contains("boris") || haystack.Contains("bcc") ||
                            haystack.Contains("optical") || haystack.Contains("continuum") ||
                            haystack.Contains("red giant") || haystack.Contains("trapcode") ||
                            haystack.Contains("prodad") || haystack.Contains("newblue");

        switch (match)
        {
            case NodeMatch.ExactUniqueID:
                return string.Equals(uid, "com.vegascreativesoftware:Stabilize", StringComparison.OrdinalIgnoreCase);
            case NodeMatch.SonyOFX:
                return uid.IndexOf("vegascreativesoftware", StringComparison.OrdinalIgnoreCase) >= 0 &&
                       uid.IndexOf("stabiliz", StringComparison.OrdinalIgnoreCase) >= 0;
            case NodeMatch.VegasName:
                return name.IndexOf("Vegas Video Stabilization", StringComparison.OrdinalIgnoreCase) >= 0 && !looksLikeBcc;
            case NodeMatch.GenericVegasLike:
                return name.IndexOf("Video Stabilization", StringComparison.OrdinalIgnoreCase) >= 0 && !looksLikeBcc;
            default:
                return false;
        }
    }

    private string WriteDiagnostic(Vegas vegas)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "VideoFX_diagnostic.txt");
        List<string> lines = new List<string>();
        lines.Add("=== VideoFX tree ===");
        CollectNodes(vegas.VideoFX, lines, 0);
        lines.Add("");
        lines.Add("=== VideoFXPackages tree ===");
        CollectNodes(vegas.VideoFXPackages, lines, 0);
        lines.Add("");
        lines.Add("=== VideoCompositeFX tree ===");
        CollectNodes(vegas.VideoCompositeFX, lines, 0);
        lines.Add("");
        lines.Add("=== PlugIns tree ===");
        CollectNodes(vegas.PlugIns, lines, 0);
        try { File.WriteAllLines(path, lines.ToArray()); } catch { }
        return path;
    }

    private void CollectNodes(PlugInNode node, List<string> lines, int depth)
    {
        if (node == null) return;

        string name = "", group = "", uniqueID = "";
        bool isContainer = false;
        try { name = node.Name; } catch { }
        try { group = node.Group; } catch { }
        try { uniqueID = node.UniqueID; } catch { }
        try { isContainer = node.IsContainer; } catch { }

        string indent = new string(' ', depth * 2);
        string marker = "";
        try { marker = IsStabilizeNode(node, NodeMatch.GenericVegasLike) ? " <== STABILIZE" : ""; } catch { }

        lines.Add(indent + "Name=" + name + " | Group=" + group + " | UniqueID=" + uniqueID + " | IsContainer=" + isContainer + marker);

        if (isContainer)
        {
            int count = 0;
            try { count = node.Count; } catch { return; }
            for (int i = 0; i < count; i++)
            {
                PlugInNode child = null;
                try { child = node.GetChild(i); } catch { continue; }
                if (child != null) CollectNodes(child, lines, depth + 1);
            }
        }
    }

    private List<VideoEvent> CollectEvents(Vegas vegas, int scope)
    {
        List<VideoEvent> list = new List<VideoEvent>();
        foreach (Track track in vegas.Project.Tracks)
        {
            if (track == null || !track.IsVideo()) continue;
            foreach (TrackEvent trackEvent in track.Events)
            {
                VideoEvent ev = trackEvent as VideoEvent;
                if (ev != null && (scope == 1 || ev.Selected)) list.Add(ev);
            }
        }
        return list;
    }

    private bool HasStabilization(VideoEvent ev, PlugInNode plugin, bool eventFx)
    {
        return GetExistingStabilization(ev, plugin, eventFx) != null;
    }

    private Effect GetExistingStabilization(VideoEvent ev, PlugInNode plugin, bool eventFx)
    {
        IEnumerable<Effect> effects = null;
        try
        {
            effects = eventFx ? ev.Effects : (ev.ActiveTake != null && ev.ActiveTake.Media != null ? ev.ActiveTake.Media.Effects : null);
        }
        catch { return null; }

        if (effects == null) return null;
        foreach (Effect effect in effects)
        {
            try { if (effect.PlugIn == plugin) return effect; } catch { }
        }
        return null;
    }

    private bool HasStabilizationAny(VideoEvent ev, PlugInNode plugin)
    {
        try
        {
            if (ev.Effects != null)
            {
                foreach (Effect effect in ev.Effects)
                {
                    try { if (effect != null && effect.PlugIn == plugin) return true; } catch { }
                }
            }
        }
        catch { }

        try
        {
            foreach (Take take in ev.Takes)
            {
                if (take == null) continue;
                Media media = take.Media;
                if (media == null || media.Effects == null) continue;
                foreach (Effect effect in media.Effects)
                {
                    try { if (effect != null && effect.PlugIn == plugin) return true; } catch { }
                }
            }
        }
        catch { }

        return false;
    }

    private void ConfigureStabilizeEffect(Effect effect, double smoothing, int modeIndex, int presetIndex)
    {
        Log("  Configure: effect=null? " + (effect == null ? "yes" : "no"));
        try
        {
            if (effect == null || !effect.IsOFX || effect.OFXEffect == null) { Log("  Configure: not OFX / no OFXEffect, skipping."); return; }
        }
        catch (Exception ex)
        {
            parameterErrors.Add("Effect check: " + ex.Message);
            Log("  Configure: effect check error: " + ex.Message);
            return;
        }

        OFXEffect ofxEffect = null;
        try { ofxEffect = effect.OFXEffect; Log("  Configure: got OFXEffect"); } catch (Exception ex) { parameterErrors.Add("OFXEffect: " + ex.Message); Log("  Configure: OFXEffect error: " + ex.Message); return; }

        string[] presetNames = { "(Default)", "full", "1.4", "7.0" };
        string presetName = presetIndex >= 0 && presetIndex < presetNames.Length ? presetNames[presetIndex] : "(Default)";
        Log("  Configure: setting preset '" + presetName + "'...");
        try { effect.Preset = presetName; Log("  Configure: preset set ok"); } catch (Exception ex) { parameterErrors.Add("Preset '" + presetName + "': " + ex.Message); Log("  Configure: preset error: " + ex.Message); }

        OFXParameter modeParam = null;
        Log("  Configure: FindParameterByName('StabilizeMode')...");
        try { modeParam = ofxEffect.FindParameterByName("StabilizeMode"); Log("  Configure: StabilizeMode param = " + (modeParam == null ? "null" : "found")); } catch (Exception ex) { parameterErrors.Add("FindParameter StabilizeMode: " + ex.Message); Log("  Configure: StabilizeMode find error: " + ex.Message); }
        if (modeParam != null)
        {
            OFXChoiceParameter modeValue = modeParam as OFXChoiceParameter;
            if (modeValue != null)
            {
                string wanted = modeIndex == 0 ? "Smooth motion" : "Freeze motion";
                Log("  Configure: setting mode '" + wanted + "'...");
                try
                {
                    foreach (OFXChoice choice in modeValue.Choices)
                    {
                        if (choice.Name != null && choice.Name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            modeValue.Value = choice;
                            break;
                        }
                    }
                    Log("  Configure: mode set ok");
                }
                catch (Exception ex) { parameterErrors.Add("Set StabilizeMode: " + ex.Message); Log("  Configure: mode error: " + ex.Message); }
            }
        }

        OFXParameter smoothParam = null;
        Log("  Configure: FindParameterByName('SmoothingMagnitude')...");
        try { smoothParam = ofxEffect.FindParameterByName("SmoothingMagnitude"); Log("  Configure: SmoothingMagnitude param = " + (smoothParam == null ? "null" : "found")); } catch (Exception ex) { parameterErrors.Add("FindParameter SmoothingMagnitude: " + ex.Message); Log("  Configure: SmoothingMagnitude find error: " + ex.Message); }
        if (smoothParam != null)
        {
            OFXDoubleParameter smoothValue = smoothParam as OFXDoubleParameter;
            if (smoothValue != null)
            {
                Log("  Configure: setting smoothing = " + smoothing + "...");
                try { smoothValue.Value = smoothing; Log("  Configure: smoothing set ok"); } catch (Exception ex) { parameterErrors.Add("Set SmoothingMagnitude: " + ex.Message); Log("  Configure: smoothing error: " + ex.Message); }
            }
        }

        Log("  Configure: AllParametersChanged()...");
        try { ofxEffect.AllParametersChanged(); Log("  Configure: AllParametersChanged ok"); } catch (Exception ex) { parameterErrors.Add("AllParametersChanged: " + ex.Message); Log("  Configure: AllParametersChanged error: " + ex.Message); }
    }

    private void TriggerAnalyze(Effect effect)
    {
        OFXEffect ofxEffect = null;
        try
        {
            if (effect == null || !effect.IsOFX || effect.OFXEffect == null) return;
            ofxEffect = effect.OFXEffect;
        }
        catch (Exception ex) { parameterErrors.Add("OFXEffect for trigger: " + ex.Message); return; }

        OFXParameter buttonParam = null;
        try { buttonParam = ofxEffect.FindParameterByName("AnalyzeMotionBasic"); } catch (Exception ex) { parameterErrors.Add("FindParameter AnalyzeMotionBasic: " + ex.Message); }
        if (buttonParam == null)
        {
            try { buttonParam = ofxEffect.FindParameterByName("AnalyzeMotion"); } catch (Exception ex) { parameterErrors.Add("FindParameter AnalyzeMotion: " + ex.Message); }
        }

        OFXPushButtonParameter button = buttonParam as OFXPushButtonParameter;
        if (button != null)
        {
            try { button.Trigger(); } catch (Exception ex) { parameterErrors.Add("Trigger Analyze: " + ex.Message); }
            try { System.Threading.Thread.Sleep(150); } catch { }
        }
    }
}
