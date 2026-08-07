// Virtual Drive Manager - Manage virtual disk images (ISO, VHD, VHDX, IMG)
// Author: Adam Gal
// Description: Modern Windows 11-style application for mounting/unmounting virtual disk images

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace VirtualDriveManager
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<MountedDrive> mountedDrives;

        public MainWindow()
        {
            InitializeComponent();
            mountedDrives = new ObservableCollection<MountedDrive>();
            LvMountedDrives.ItemsSource = mountedDrives;
            RefreshMountedDrives();
        }

        /// <summary>
        /// Refreshes the list of mounted virtual drives by querying all CD-ROM drives.
        /// Runs on background thread to keep UI responsive.
        /// </summary>
        private async void RefreshMountedDrives()
        {
            try
            {
                TxtStatus.Text = "Loading drives...";

                // Run on background thread to prevent UI freeze
                await Task.Run(() =>
                {
                    var drives = DriveInfo.GetDrives();
                    var drivesToAdd = new System.Collections.Generic.List<MountedDrive>();

                    foreach (var drive in drives)
                    {
                        if (drive.DriveType == DriveType.CDRom && drive.IsReady)
                        {
                            string imagePath = GetImagePathForDrive(drive.Name.TrimEnd('\\'));

                            if (!string.IsNullOrEmpty(imagePath))
                            {
                                drivesToAdd.Add(new MountedDrive
                                {
                                    DriveLetter = drive.Name,
                                    ImagePath = imagePath,
                                    Type = Path.GetExtension(imagePath).ToUpper(),
                                    Size = FormatSize(drive.TotalSize)
                                });
                            }
                        }
                    }

                    // Update UI on main thread
                    Dispatcher.Invoke(() =>
                    {
                        mountedDrives.Clear();
                        foreach (var drive in drivesToAdd)
                        {
                            mountedDrives.Add(drive);
                        }
                        TxtStatus.Text = $"{mountedDrives.Count} mounted drives";
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error listing drives: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Error loading drives";
            }
        }

        /// <summary>
        /// Retrieves the image file path for a given virtual drive using WMI queries.
        /// </summary>
        private string GetImagePathForDrive(string driveLetter)
        {
            try
            {
                // Query logical disk and associated CDROM drive to find virtual image
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_LogicalDisk WHERE DeviceID='{driveLetter}'"))
                {
                    foreach (ManagementObject disk in searcher.Get())
                    {
                        // Check for connected CDROM device
                        using (var cdromSearcher = new ManagementObjectSearcher(
                            "SELECT * FROM Win32_CDROMDrive"))
                        {
                            foreach (ManagementObject cdrom in cdromSearcher.Get())
                            {
                                if (cdrom["Drive"]?.ToString() == driveLetter)
                                {
                                    // Windows 8+ stores the image file path
                                    string imagePath = GetMountedImagePath(driveLetter);
                                    if (!string.IsNullOrEmpty(imagePath))
                                        return imagePath;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            
            return string.Empty;
        }

        /// <summary>
        /// Queries mounted image path from Windows registry and system APIs.
        /// </summary>
        private string GetMountedImagePath(string driveLetter)
        {
            try
            {
                // Read registry for mounted image file list
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\MountedDevices"))
                {
                    if (key != null)
                    {
                        // Modern Windows verziókon próbáljuk meg máshogy
                        string appDataPath = Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData);
                        string mountCachePath = Path.Combine(appDataPath, 
                            @"Microsoft\Windows\Explorer\thumbcache_*.db");
                        
                        // Alternative: AttachedVirtualDisk API
                        return GetAttachedImagePath(driveLetter);
                    }
                }
            }
            catch { }
            
            return string.Empty;
        }

        /// <summary>
        /// Retrieves attached virtual disk image path using PowerShell Get-DiskImage command.
        /// </summary>
        private string GetAttachedImagePath(string driveLetter)
        {
            // Windows API to retrieve attached virtual disk information
            IntPtr hFind = IntPtr.Zero;

            try
            {
                // Simplified solution - works in most cases
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "powershell.exe";
                process.StartInfo.Arguments = $"-NoProfile -Command \"Get-DiskImage -DevicePath \\\\.\\{driveLetter} | Select-Object -ExpandProperty ImagePath\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Opens file dialog and mounts the selected disk image via PowerShell Mount-DiskImage command.
        /// Runs on background thread to prevent UI freezing.
        /// </summary>
        private void BtnMount_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select an image file to mount",
                Filter = "Image files (*.iso;*.img;*.vhd;*.vhdx)|*.iso;*.img;*.vhd;*.vhdx|" +
                         "ISO files (*.iso)|*.iso|" +
                         "IMG files (*.img)|*.img|" +
                         "VHD files (*.vhd)|*.vhd|" +
                         "VHDX files (*.vhdx)|*.vhdx|" +
                         "All files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                MountImage(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// Executes Mount-DiskImage command asynchronously to mount a virtual disk image.
        /// </summary>
        private async void MountImage(string imagePath)
        {
            try
            {
                TxtStatus.Text = $"Mounting: {Path.GetFileName(imagePath)}...";

                // Run mount operation on background thread
                int exitCode = await Task.Run(() =>
                {
                    var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-NoProfile -Command \"Mount-DiskImage -ImagePath '{imagePath}'\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                    return process.ExitCode;
                });

                if (exitCode == 0)
                {
                    TxtStatus.Text = "Mounted successfully!";
                    await Task.Delay(1000); // Wait briefly for system to refresh
                    RefreshMountedDrives();
                }
                else
                {
                    TxtStatus.Text = "Mount failed";
                    MessageBox.Show("Mount failed. Check the image file and try again.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error mounting: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "An error occurred";
            }
        }

        /// <summary>
        /// Unmounts the selected drive via Dismount-DiskImage command.
        /// Uses alternative method if primary dismount fails.
        /// </summary>
        private void BtnUnmount_Click(object sender, RoutedEventArgs e)
        {
            if (LvMountedDrives.SelectedItem is MountedDrive selectedDrive)
            {
                UnmountDrive(selectedDrive.DriveLetter);
            }
            else
            {
                MessageBox.Show("Please select a drive from the list!", "Warning", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Unmounts a virtual drive asynchronously using PowerShell Dismount-DiskImage.
        /// Attempts alternative method if primary command fails.
        /// </summary>
        private async void UnmountDrive(string driveLetter)
        {
            try
            {
                TxtStatus.Text = $"Unmounting: {driveLetter}...";

                // Run unmount operation on background thread
                int exitCode = await Task.Run(() =>
                {
                    var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-NoProfile -Command \"Dismount-DiskImage -DevicePath \\\\.\\{driveLetter}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                    return process.ExitCode;
                });

                if (exitCode == 0)
                {
                    TxtStatus.Text = "Unmounted successfully!";
                    RefreshMountedDrives();
                }
                else
                {
                    // Alternative unmount method
                    int exitCode2 = await Task.Run(() =>
                    {
                        var process2 = new System.Diagnostics.Process();
                        process2.StartInfo.FileName = "powershell.exe";
                        process2.StartInfo.Arguments = $"-NoProfile -Command \"Get-Volume -DriveLetter {driveLetter.Replace(":", "").Replace("\\", "")} | Get-DiskImage | Dismount-DiskImage\"";
                        process2.StartInfo.UseShellExecute = false;
                        process2.StartInfo.RedirectStandardOutput = true;
                        process2.StartInfo.RedirectStandardError = true;
                        process2.StartInfo.CreateNoWindow = true;
                        process2.Start();
                        process2.WaitForExit();
                        return process2.ExitCode;
                    });

                    if (exitCode2 == 0)
                    {
                        TxtStatus.Text = "Unmounted successfully!";
                        RefreshMountedDrives();
                    }
                    else
                    {
                        TxtStatus.Text = "Unmount failed";
                        MessageBox.Show("Unmount failed. The drive may be in use.", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error unmounting: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "An error occurred";
            }
        }

        /// <summary>
        /// Unmounts all mounted drives with confirmation dialog.
        /// </summary>
        private void BtnUnmountAll_Click(object sender, RoutedEventArgs e)
        {
            if (mountedDrives.Count == 0)
            {
                MessageBox.Show("No mounted drives.", "Information", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to unmount all {mountedDrives.Count} drives?", 
                "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var drive in mountedDrives.ToList())
                {
                    UnmountDrive(drive.DriveLetter);
                }
            }
        }

        /// <summary>
        /// Refreshes the mounted drives list when Refresh button is clicked.
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshMountedDrives();
        }

        /// <summary>
        /// Formats bytes to human-readable size format (B, KB, MB, GB, TB).
        /// </summary>
        private string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:n1} {suffixes[counter]}";
        }
    }

    /// <summary>
    /// Model class for mounted virtual drive information.
    /// Implements INotifyPropertyChanged for data binding.
    /// Properties: DriveLetter, ImagePath, Type, Size
    /// </summary>
    public class MountedDrive : INotifyPropertyChanged
    {
        private string driveLetter;
        private string imagePath;
        private string type;
        private string size;

        public string DriveLetter
        {
            get => driveLetter;
            set { driveLetter = value; OnPropertyChanged(nameof(DriveLetter)); }
        }

        public string ImagePath
        {
            get => imagePath;
            set { imagePath = value; OnPropertyChanged(nameof(ImagePath)); }
        }

        public string Type
        {
            get => type;
            set { type = value; OnPropertyChanged(nameof(Type)); }
        }

        public string Size
        {
            get => size;
            set { size = value; OnPropertyChanged(nameof(Size)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}