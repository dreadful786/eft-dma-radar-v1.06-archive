using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using DarkModeForms;

namespace radar_launcher
{
    public partial class LauncherForm : Form
    {
        private readonly string _eftRadarPath;
        private readonly string _eftRadarNonRotatedPath;
        private readonly string _arenaRadarPath;
        private readonly DarkModeCS _darkmode;

        public LauncherForm()
        {
            InitializeComponent();

            SetDarkMode(ref _darkmode);
            // Dynamically locate the solution directory
            string solutionDir = GetSolutionDirectory();

            #if DEBUG
            string buildConfig = "Debug";
            #else
            string buildConfig = "Release";
            #endif

            _eftRadarPath = Path.Combine(solutionDir, $@"eft-dma-radar\bin\x64\{buildConfig}\net9.0-windows\eft-dma-radar.exe");
            _eftRadarNonRotatedPath = Path.Combine(solutionDir, $@"eft-dma-radar-non-rotated-maps\bin\x64\{buildConfig}\net9.0-windows\eft-dma-radar-non-rotated-maps.exe");
            _arenaRadarPath = Path.Combine(solutionDir, $@"arena-dma-radar\bin\x64\{buildConfig}\net9.0-windows\arena-dma-radar.exe");

            // Debug.WriteLine($"EFT Radar Path: {_eftRadarPath}");
            // Debug.WriteLine($"EFT Radar NonRotated Path: {_eftRadarNonRotatedPath}");
            // Debug.WriteLine($"Arena Radar Path: {_arenaRadarPath}");

            // Verify if executable files exist and update UI accordingly
            btnEftRadar.Enabled = File.Exists(_eftRadarPath);
            btnEftRadarNonRotated.Enabled = File.Exists(_eftRadarNonRotatedPath);
            btnArenaRadar.Enabled = File.Exists(_arenaRadarPath);

            if (!btnEftRadar.Enabled)
            {
                toolTip1.SetToolTip(btnEftRadar, "Executable not found at: " + _eftRadarPath);
            }

            if (!btnEftRadarNonRotated.Enabled)
            {
                toolTip1.SetToolTip(btnEftRadarNonRotated, "Executable not found at: " + _eftRadarNonRotatedPath);
            }

            if (!btnArenaRadar.Enabled)
            {
                toolTip1.SetToolTip(btnArenaRadar, "Executable not found at: " + _arenaRadarPath);
            }

            /*
            if (!btnArenaRadar.Enabled)
            {
                toolTip1.SetToolTip(btnArenaRadar, "Executable not found at: " + _arenaRadarPath);
            }
            */
        }
        /// <summary>
        /// Set Dark Mode on startup.
        /// </summary>
        /// <param name="darkmode"></param>
        private void SetDarkMode(ref DarkModeCS darkmode)
        {
            darkmode = new DarkModeCS(this);
            // Additional customization can be done here if needed
        }
        private void btnEftRadar_Click(object sender, EventArgs e)
        {
            // Try to get the saved game mode preference
            bool savedIsPve = true; // Default to PvE mode
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "eft-dma-radar", "Config-EFT.json");

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.TryGetProperty("isPveMode", out var isPveElement))
                    {
                        savedIsPve = isPveElement.GetBoolean();
                    }
                }
                catch
                {
                    // Use default if there's any issue reading the config
                }
            }

            // Create and configure dialog
            using var gameModeDialog = new Form()
            {
                Text = "Select Game Mode",
                ClientSize = new Size(200, 80),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false
            };

            // Apply dark mode to the dialog
            DarkModeCS dialogDarkMode = new DarkModeCS(gameModeDialog);

            // Create radio buttons
            var pveRadio = new RadioButton()
            {
                Text = "PvE Mode",
                Location = new Point(20, 10),
                AutoSize = true,
                Checked = savedIsPve, // Use saved preference
                ForeColor = System.Drawing.Color.White // Ensure text is visible in dark mode
            };

            var pvpRadio = new RadioButton()
            {
                Text = "PvP Mode",
                Location = new Point(105, 10),
                AutoSize = true,
                Checked = !savedIsPve, // Use saved preference
                ForeColor = System.Drawing.Color.White // Ensure text is visible in dark mode
            };

            // Create OK and Cancel buttons
            var okButton = new Button()
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(20, 40),
                Size = new Size(80, 30)
            };

            var cancelButton = new Button()
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(105, 40),
                Size = new Size(80, 30)
            };

            // Add controls to form
            gameModeDialog.Controls.AddRange(new Control[] { pveRadio, pvpRadio, okButton, cancelButton });
            gameModeDialog.AcceptButton = okButton;
            gameModeDialog.CancelButton = cancelButton;

            // Show dialog and check result
            if (gameModeDialog.ShowDialog(this) == DialogResult.OK)
            {
                bool isPve = pveRadio.Checked;
                LaunchEftRadar(isPve);
            }
        }


        private void btnEftRadarNonRotated_Click(object sender, EventArgs e)
        {
            LaunchExecutable(_eftRadarNonRotatedPath);
        }
        private void btnArenaRadar_Click(object sender, EventArgs e)
        {
            LaunchExecutable(_arenaRadarPath);
        }
        private void LaunchExecutable(string path)
        {
            try
            {
                // Set the working directory to the directory of the executable
                string workingDirectory = Path.GetDirectoryName(path);

                ProcessStartInfo startInfo = new()
                {
                    FileName = path,
                    WorkingDirectory = workingDirectory, // Set the working directory
                    UseShellExecute = true
                };

                Process.Start(startInfo);

                // Close the launcher after starting the selected application
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching application: {ex.Message}",
                    "Launch Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private string GetSolutionDirectory()
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(currentDir))
            {
                // Check for a known file or folder in the solution directory
                if (File.Exists(Path.Combine(currentDir, "eft-dma-radar.sln")) ||
                    Directory.Exists(Path.Combine(currentDir, "eft-dma-radar")))
                {
                    return currentDir;
                }

                // Move up one directory
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            throw new DirectoryNotFoundException("Solution directory could not be located.");
        }

        private void LaunchEftRadar(bool isPve)
        {
            try
            {
                string path = _eftRadarPath;
                // Set the working directory to the directory of the executable
                string workingDirectory = Path.GetDirectoryName(path);

                ProcessStartInfo startInfo = new()
                {
                    FileName = path,
                    WorkingDirectory = workingDirectory, // Set the working directory
                    UseShellExecute = true,
                    Arguments = isPve ? "pve" : "pvp" // Pass game mode as command-line argument
                };

                Process.Start(startInfo);

                // Close the launcher after starting the selected application
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching application: {ex.Message}",
                    "Launch Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}