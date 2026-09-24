using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace VideoBatchSplitter;

public partial class MainWindow : Window
{
    private readonly string[] videoExtensions =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm",
        ".m4v", ".flv", ".mpeg", ".mpg", ".3gp", ".ts"
    };

    private List<string> currentVideos = new();

    private CancellationTokenSource? cancellationTokenSource;


    public MainWindow()
    {
        InitializeComponent();
    }


    // ============================================================
    // SOURCE FOLDER
    // ============================================================

    private void BrowseSource_Click(
        object sender,
        RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select the folder containing your videos",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            SourceFolderTextBox.Text = dialog.SelectedPath;
            RefreshInfo();
        }
    }


    private void SourceFolderTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (IsInitialized)
            RefreshInfo();
    }


    private void BatchSizeTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (IsInitialized)
            RefreshInfo();
    }


    private void RefreshInfo()
    {
        string folder =
            SourceFolderTextBox.Text.Trim();

        if (!Directory.Exists(folder))
        {
            currentVideos.Clear();

            FoundText.Text = "0";
            FoldersText.Text = "0";

            return;
        }

        try
        {
            currentVideos = Directory
                .EnumerateFiles(
                    folder,
                    "*.*",
                    SearchOption.TopDirectoryOnly)
                .Where(f =>
                    videoExtensions.Contains(
                        Path.GetExtension(f),
                        StringComparer.OrdinalIgnoreCase))
                .ToList();

            FoundText.Text =
                currentVideos.Count.ToString();

            if (int.TryParse(
                    BatchSizeTextBox.Text,
                    out int batchSize) &&
                batchSize > 0)
            {
                int folderCount =
                    (currentVideos.Count + batchSize - 1)
                    / batchSize;

                FoldersText.Text =
                    folderCount.ToString();
            }
            else
            {
                FoldersText.Text = "0";
            }
        }
        catch
        {
            currentVideos.Clear();

            FoundText.Text = "0";
            FoldersText.Text = "0";
        }
    }


    // ============================================================
    // PERFORMANCE CHECK
    // ============================================================

    private void PerformanceCheck_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowPerformanceCheckWindow();
    }


    private void ShowPerformanceCheckWindow()
    {
        var processes = GetUserApplications();

        var window = new Window
        {
            Title = "Performance Check",
            Width = 620,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResize,
            Background = System.Windows.Media.Brushes.White
        };


        var mainGrid = new Grid
        {
            Margin = new Thickness(20)
        };

        mainGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        mainGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        mainGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });


        var title = new TextBlock
        {
            Text = "Applications using system resources",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(
                        23, 32, 42))
        };

        Grid.SetRow(title, 0);

        mainGrid.Children.Add(title);


        var description = new TextBlock
        {
            Text =
                "These are user applications with visible windows. " +
                "Select only applications you are sure you want to close.",
            FontSize = 13,
            Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(
                        104, 115, 125)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 34, 0, 10)
        };

        Grid.SetRow(description, 0);

        mainGrid.Children.Add(description);


        var list = new ListBox
        {
            Margin = new Thickness(0, 70, 0, 15)
        };

        foreach (var processInfo in processes)
        {
            var checkBox = new CheckBox
            {
                Content =
                    $"{processInfo.Name}    " +
                    $"RAM: {processInfo.MemoryMB:N0} MB",
                Tag = processInfo,
                Padding = new Thickness(6),
                Margin = new Thickness(2)
            };

            list.Items.Add(checkBox);
        }

        Grid.SetRow(list, 1);

        mainGrid.Children.Add(list);


        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };


        var closeSelectedButton = new Button
        {
            Content = "Close Selected",
            Width = 125,
            Height = 40,
            Margin = new Thickness(0, 0, 10, 0)
        };


        var continueButton = new Button
        {
            Content = "Continue",
            Width = 100,
            Height = 40,
            Margin = new Thickness(0, 0, 10, 0)
        };


        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90,
            Height = 40
        };


        closeSelectedButton.Click += (_, _) =>
        {
            var selected =
                list.Items
                    .OfType<CheckBox>()
                    .Where(x => x.IsChecked == true)
                    .Select(x => x.Tag as ProcessInfo)
                    .Where(x => x != null)
                    .Cast<ProcessInfo>()
                    .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    window,
                    "Select at least one application.",
                    "Nothing selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            var confirmation =
                MessageBox.Show(
                    window,
                    $"Close {selected.Count} selected application(s)?\n\n" +
                    "Make sure you have saved your work first.",
                    "Confirm",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.OK)
                return;


            int closed = 0;

            foreach (var item in selected)
            {
                try
                {
                    using var process =
                        Process.GetProcessById(item.ProcessId);

                    if (process.HasExited)
                        continue;

                    bool requested =
                        process.CloseMainWindow();

                    if (requested)
                        closed++;
                }
                catch
                {
                    // Process may have already closed.
                }
            }


            MessageBox.Show(
                window,
                $"{closed} application(s) were asked to close.\n\n" +
                "Applications with unsaved work may remain open.",
                "Performance Check",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };


        continueButton.Click += (_, _) =>
        {
            window.DialogResult = true;
            window.Close();
        };


        cancelButton.Click += (_, _) =>
        {
            window.DialogResult = false;
            window.Close();
        };


        buttonPanel.Children.Add(closeSelectedButton);
        buttonPanel.Children.Add(continueButton);
        buttonPanel.Children.Add(cancelButton);

        Grid.SetRow(buttonPanel, 2);

        mainGrid.Children.Add(buttonPanel);


        window.Content = mainGrid;

        window.ShowDialog();
    }


    private List<ProcessInfo> GetUserApplications()
    {
        var result = new List<ProcessInfo>();

        int currentProcessId =
            Environment.ProcessId;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == currentProcessId)
                    continue;

                if (process.SessionId !=
                    Process.GetCurrentProcess().SessionId)
                    continue;

                if (process.MainWindowHandle ==
                    IntPtr.Zero)
                    continue;

                if (string.Equals(
                        process.ProcessName,
                        "explorer",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                long memory =
                    process.WorkingSet64;

                double memoryMB =
                    memory / 1024.0 / 1024.0;

                result.Add(
                    new ProcessInfo
                    {
                        ProcessId = process.Id,
                        Name = process.ProcessName,
                        MemoryMB = memoryMB
                    });
            }
            catch
            {
                // Access denied / process exited.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result
            .OrderByDescending(x => x.MemoryMB)
            .Take(30)
            .ToList();
    }


    // ============================================================
    // START
    // ============================================================

    private async void Start_Click(
        object sender,
        RoutedEventArgs e)
    {
        string source =
            SourceFolderTextBox.Text.Trim();


        if (!Directory.Exists(source))
        {
            MessageBox.Show(
                "Please select a valid source folder.",
                "Invalid folder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }


        if (!int.TryParse(
                BatchSizeTextBox.Text,
                out int batchSize) ||
            batchSize <= 0)
        {
            MessageBox.Show(
                "Videos per folder must be a positive number.",
                "Invalid value",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }


        RefreshInfo();


        if (currentVideos.Count == 0)
        {
            MessageBox.Show(
                "No supported video files were found in the selected folder.",
                "No videos",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }


        bool isCopyMode =
            CopyRadioButton.IsChecked == true;

        bool isRandomMode =
            RandomRadioButton.IsChecked == true;


        long totalBytes =
            GetTotalFileSize(currentVideos);


        int total =
            currentVideos.Count;

        int folderCount =
            (total + batchSize - 1) / batchSize;


        string operationText =
            isCopyMode
                ? "COPY - original files will remain."
                : "MOVE - original files will be moved.";


        string orderText =
            isRandomMode
                ? "RANDOM - videos will be shuffled."
                : "ORIGINAL - current file order will be used.";


        string sizeText =
            FormatBytes(totalBytes);


        var result =
            MessageBox.Show(
                $"Found {total:N0} videos.\n\n" +
                $"Total size: {sizeText}\n" +
                $"Videos per folder: {batchSize:N0}\n" +
                $"Folders to create: {folderCount:N0}\n\n" +
                $"Operation:\n{operationText}\n\n" +
                $"Order:\n{orderText}\n\n" +
                "Do you want to continue?",
                "Confirm operation",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);


        if (result != MessageBoxResult.OK)
            return;


        // --------------------------------------------------------
        // Performance check
        // --------------------------------------------------------

        var performanceResult =
            MessageBox.Show(
                "Would you like to check running applications " +
                "before starting?\n\n" +
                "You can close selected applications to reduce " +
                "CPU/RAM/disk activity.",
                "Performance Check",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);


        if (performanceResult ==
            MessageBoxResult.Cancel)
        {
            return;
        }


        if (performanceResult ==
            MessageBoxResult.Yes)
        {
            ShowPerformanceCheckWindow();
        }


        // --------------------------------------------------------
        // Prepare
        // --------------------------------------------------------

        cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;


        SetControlsForProcessing(true);


        StatusText.Text = "Preparing...";
        CurrentFileText.Text = "Preparing files...";

        ProgressBar.Value = 0;
        CurrentFileProgressBar.Value = 0;

        ProgressPercentText.Text = "0%";
        CurrentFileProgressText.Text = "0%";

        TransferredText.Text =
            $"0 MB / {FormatBytes(totalBytes)}";

        SpeedText.Text = "0 MB/s";
        EtaText.Text = "--:--";


        try
        {
            var videos =
                currentVideos.ToList();


            if (isRandomMode)
            {
                Shuffle(videos);
            }


            var progress =
                new Progress<ProgressInfo>(info =>
                {
                    ProgressBar.Value =
                        info.Percent;

                    ProgressPercentText.Text =
                        $"{info.Percent:0.0}%";


                    CurrentFileProgressBar.Value =
                        info.CurrentFilePercent;

                    CurrentFileProgressText.Text =
                        $"{info.CurrentFilePercent:0.0}%";


                    CurrentFileText.Text =
                        $"{info.ProcessedFiles:N0} / " +
                        $"{info.TotalFiles:N0}    " +
                        $"{info.FileName}";


                    TransferredText.Text =
                        $"{FormatBytes(info.ProcessedBytes)} / " +
                        $"{FormatBytes(info.TotalBytes)}";


                    SpeedText.Text =
                        $"{info.SpeedMBps:0.0} MB/s";


                    EtaText.Text =
                        FormatTime(info.RemainingSeconds);


                    StatusText.Text =
                        $"Processing... {info.Percent:0.0}%";
                });


            await ProcessVideosAsync(
                videos,
                source,
                batchSize,
                isCopyMode,
                progress,
                cancellationToken);


            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;


            ProgressBar.Value = 100;
            ProgressPercentText.Text = "100%";

            StatusText.Text = "Completed";

            CurrentFileProgressBar.Value = 100;
            CurrentFileProgressText.Text = "100%";

            CurrentFileText.Text =
                $"Finished - {total:N0} videos processed.";

            SpeedText.Text = "Done";
            EtaText.Text = "00:00";


            RefreshInfo();


            MessageBox.Show(
                $"Done!\n\n" +
                $"{total:N0} videos were " +
                $"{(isCopyMode ? "copied" : "moved")} " +
                $"into {folderCount:N0} folders.\n\n" +
                $"Total size: {sizeText}\n" +
                $"Order: {(isRandomMode ? "Random" : "Original")}",
                "Completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Cancelled";

            CurrentFileText.Text =
                "Operation cancelled by user.";

            EtaText.Text = "--:--";


            MessageBox.Show(
                "The operation was cancelled.\n\n" +
                "Files that were already processed remain processed.",
                "Cancelled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error";

            CurrentFileText.Text =
                "An error occurred.";


            MessageBox.Show(
                $"An error occurred:\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            SetControlsForProcessing(false);

            RefreshInfo();
        }
    }


    // ============================================================
    // CANCEL
    // ============================================================

    private async void Cancel_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (cancellationTokenSource == null)
            return;


        var result =
            MessageBox.Show(
                "Cancel the current operation?\n\n" +
                "Files already processed will remain processed.",
                "Cancel operation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);


        if (result != MessageBoxResult.Yes)
            return;


        CancelButton.IsEnabled = false;

        StatusText.Text = "Cancelling...";
        CurrentFileText.Text =
            "Stopping operation safely...";


        cancellationTokenSource.Cancel();


        // Let the UI remain responsive while the
        // background operation observes cancellation.
        await Task.Yield();
    }


    // ============================================================
    // PROCESS
    // ============================================================

    private async Task ProcessVideosAsync(
        List<string> videos,
        string source,
        int batchSize,
        bool isCopyMode,
        IProgress<ProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        int totalFiles =
            videos.Count;

        long totalBytes =
            GetTotalFileSize(videos);

        long processedBytes = 0;

        int processedFiles = 0;

        int batchNumber = 1;


        var stopwatch =
            Stopwatch.StartNew();


        for (
            int i = 0;
            i < videos.Count;
            i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();


            string batchFolder =
                Path.Combine(
                    source,
                    $"Batch_{batchNumber:000}");


            Directory.CreateDirectory(
                batchFolder);


            int currentBatchEnd =
                Math.Min(
                    i + batchSize,
                    videos.Count);


            for (
                int j = i;
                j < currentBatchEnd;
                j++)
            {
                cancellationToken.ThrowIfCancellationRequested();


                string file =
                    videos[j];


                string fileName =
                    Path.GetFileName(file);


                string destination =
                    Path.Combine(
                        batchFolder,
                        fileName);


                if (File.Exists(destination))
                {
                    destination =
                        GetUniqueFileName(destination);
                }


                long fileSize =
                    new FileInfo(file).Length;


                long fileProcessedBytes = 0;


                if (isCopyMode)
                {
                    try
                    {
                        await CopyFileAsync(
                            file,
                            destination,
                            fileSize,
                            cancellationToken,
                            async bytesCopied =>
                            {
                                fileProcessedBytes =
                                    bytesCopied;


                                ReportProgress(
                                    progress,
                                    stopwatch,
                                    processedBytes,
                                    fileProcessedBytes,
                                    fileSize,
                                    totalBytes,
                                    processedFiles,
                                    totalFiles,
                                    fileName);

                                await Task.CompletedTask;
                            });
                    }
                    catch
                    {
                        // If cancellation/error occurs,
                        // remove incomplete destination.
                        if (File.Exists(destination))
                        {
                            try
                            {
                                File.Delete(destination);
                            }
                            catch
                            {
                                // Ignore cleanup failure.
                            }
                        }

                        throw;
                    }
                }
                else
                {
                    File.Move(
                        file,
                        destination,
                        overwrite: false);

                    fileProcessedBytes =
                        fileSize;


                    ReportProgress(
                        progress,
                        stopwatch,
                        processedBytes,
                        fileProcessedBytes,
                        fileSize,
                        totalBytes,
                        processedFiles,
                        totalFiles,
                        fileName);
                }


                processedBytes += fileSize;

                processedFiles++;


                ReportProgress(
                    progress,
                    stopwatch,
                    processedBytes,
                    fileSize,
                    fileSize,
                    totalBytes,
                    processedFiles,
                    totalFiles,
                    fileName);
            }


            batchNumber++;
        }
    }


    // ============================================================
    // FAST COPY
    // ============================================================

    private static async Task CopyFileAsync(
        string source,
        string destination,
        long fileSize,
        CancellationToken cancellationToken,
        Func<long, Task> progressCallback)
    {
        const int bufferSize =
            4 * 1024 * 1024;


        var sourceOptions =
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = bufferSize,
                Options =
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan
            };


        var destinationOptions =
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = bufferSize,
                Options =
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan
            };


        await using var input =
            new FileStream(
                source,
                sourceOptions);


        await using var output =
            new FileStream(
                destination,
                destinationOptions);


        byte[] buffer =
            new byte[bufferSize];


        long copied = 0;


        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();


            int read =
                await input.ReadAsync(
                    buffer.AsMemory(
                        0,
                        buffer.Length),
                    cancellationToken);


            if (read == 0)
                break;


            await output.WriteAsync(
                buffer.AsMemory(
                    0,
                    read),
                cancellationToken);


            copied += read;


            await progressCallback(copied);
        }


        await output.FlushAsync(
            cancellationToken);
    }


    // ============================================================
    // PROGRESS
    // ============================================================

    private static void ReportProgress(
        IProgress<ProgressInfo> progress,
        Stopwatch stopwatch,
        long processedBytes,
        long currentFileBytes,
        long currentFileSize,
        long totalBytes,
        int processedFiles,
        int totalFiles,
        string fileName)
    {
        double percent =
            totalBytes <= 0
                ? 100
                : processedBytes * 100.0 / totalBytes;


        double currentFilePercent =
            currentFileSize <= 0
                ? 100
                : currentFileBytes * 100.0 /
                  currentFileSize;


        double elapsedSeconds =
            stopwatch.Elapsed.TotalSeconds;


        double speedBytesPerSecond =
            elapsedSeconds <= 0
                ? 0
                : processedBytes /
                  elapsedSeconds;


        double remainingSeconds =
            speedBytesPerSecond <= 0
                ? 0
                : (totalBytes - processedBytes) /
                  speedBytesPerSecond;


        double speedMBps =
            speedBytesPerSecond /
            1024.0 /
            1024.0;


        progress.Report(
            new ProgressInfo
            {
                ProcessedBytes =
                    processedBytes,

                TotalBytes =
                    totalBytes,

                ProcessedFiles =
                    processedFiles,

                TotalFiles =
                    totalFiles,

                Percent =
                    Math.Min(
                        100,
                        Math.Max(
                            0,
                            percent)),

                CurrentFilePercent =
                    Math.Min(
                        100,
                        Math.Max(
                            0,
                            currentFilePercent)),

                SpeedMBps =
                    speedMBps,

                RemainingSeconds =
                    remainingSeconds,

                FileName =
                    fileName
            });
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private static long GetTotalFileSize(
        IEnumerable<string> files)
    {
        long total = 0;

        foreach (string file in files)
        {
            try
            {
                total +=
                    new FileInfo(file).Length;
            }
            catch
            {
                // Ignore files that disappear
                // while scanning.
            }
        }

        return total;
    }


    private static string FormatBytes(
        long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:0.0} KB";

        if (bytes < 1024L * 1024L * 1024L)
            return $"{bytes / 1024.0 / 1024.0:0.0} MB";

        return
            $"{bytes / 1024.0 / 1024.0 / 1024.0:0.00} GB";
    }


    private static string FormatTime(
        double seconds)
    {
        if (seconds <= 0)
            return "--:--";


        var time =
            TimeSpan.FromSeconds(seconds);


        if (time.TotalHours >= 1)
        {
            return
                $"{(int)time.TotalHours:00}:" +
                $"{time.Minutes:00}:" +
                $"{time.Seconds:00}";
        }


        return
            $"{time.Minutes:00}:" +
            $"{time.Seconds:00}";
    }


    private static void Shuffle<T>(
        IList<T> list)
    {
        var random =
            Random.Shared;


        for (
            int i = list.Count - 1;
            i > 0;
            i--)
        {
            int j =
                random.Next(i + 1);


            (list[i], list[j]) =
                (list[j], list[i]);
        }
    }


    private static string GetUniqueFileName(
        string path)
    {
        string directory =
            Path.GetDirectoryName(path)!;


        string name =
            Path.GetFileNameWithoutExtension(path);


        string extension =
            Path.GetExtension(path);


        int counter = 1;


        string candidate;


        do
        {
            candidate =
                Path.Combine(
                    directory,
                    $"{name}_{counter}{extension}");

            counter++;

        }
        while (File.Exists(candidate));


        return candidate;
    }


    private void SetControlsForProcessing(
        bool processing)
    {
        StartButton.IsEnabled =
            !processing;

        CancelButton.IsEnabled =
            processing;

        BrowseButton.IsEnabled =
            !processing;

        PerformanceCheckButton.IsEnabled =
            !processing;

        BatchSizeTextBox.IsEnabled =
            !processing;

        MoveRadioButton.IsEnabled =
            !processing;

        CopyRadioButton.IsEnabled =
            !processing;

        RandomRadioButton.IsEnabled =
            !processing;

        OriginalRadioButton.IsEnabled =
            !processing;

        SourceFolderTextBox.IsEnabled =
            !processing;
    }
}


// ================================================================
// DATA CLASSES
// ================================================================

public class ProgressInfo
{
    public long ProcessedBytes { get; set; }

    public long TotalBytes { get; set; }

    public int ProcessedFiles { get; set; }

    public int TotalFiles { get; set; }

    public double Percent { get; set; }

    public double CurrentFilePercent { get; set; }

    public double SpeedMBps { get; set; }

    public double RemainingSeconds { get; set; }

    public string FileName { get; set; } = "";
}


public class ProcessInfo
{
    public int ProcessId { get; set; }

    public string Name { get; set; } = "";

    public double MemoryMB { get; set; }
}