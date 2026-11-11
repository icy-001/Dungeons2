using CommunityToolkit.Mvvm.ComponentModel;
using Dungeons2.Services;
using System.ComponentModel;

namespace Dungeons2.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public GoogleSheetsService Sheets => _sheets;

        public SettingsViewModel() : this(GoogleSheetsService.Instance) { }

        internal SettingsViewModel(GoogleSheetsService sheets)
        {
            _sheets = sheets;
            StartCellInput = Settings.Instance.SpreadsheetStartCell;
            Sheets.PropertyChanged += RefreshSheetsState;
            Settings.Instance.PropertyChanged += RefreshSheetsState;
        }

        private readonly GoogleSheetsService _sheets;

        [ObservableProperty] public partial string StartCellInput { get; set; } = "A2";
        public bool IsStartCellValid => GoogleSheetsStartCell.TryParse(StartCellInput, out _);
        public bool CanApplyStartCell => CanUseSheet && IsStartCellValid &&
            GoogleSheetsStartCell.Parse(StartCellInput).Address != Settings.Instance.SpreadsheetStartCell;
        public string StartCellHint => !IsStartCellValid ? "Enter one cell, such as A2 or C5 (not a range)."
            : $"First data cell: {GoogleSheetsStartCell.Parse(StartCellInput).Address}. Floor times only, in column " +
              $"{GoogleSheetsStartCell.ColumnName(GoogleSheetsStartCell.Parse(StartCellInput).Column)}. " +
              "Existing data is kept; times append below it. Apply changes for new floors only.";
        public string DestinationSummary => $"Destination: first visible worksheet · starting at {Settings.Instance.SpreadsheetStartCell}";

        partial void OnStartCellInputChanged(string value)
        {
            OnPropertyChanged(nameof(IsStartCellValid));
            OnPropertyChanged(nameof(CanApplyStartCell));
            OnPropertyChanged(nameof(StartCellHint));
            ApplySheetsStartCellCommand.NotifyCanExecuteChanged();
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanApplyStartCell))]
        private void ApplySheetsStartCell()
        {
            Settings.Instance.SpreadsheetStartCell = GoogleSheetsStartCell.Parse(StartCellInput).Address;
            StartCellInput = Settings.Instance.SpreadsheetStartCell;
            Settings.Instance.Save();
        }

        public bool CanManageSheets => Sheets.IsAvailable && !Sheets.IsBusy;
        public bool CanUseSheet => CanManageSheets && Sheets.IsConnected;
        public bool CanRetrySheets => CanUseSheet && Settings.Instance.LogToSheet && Sheets.SelectedPendingCount > 0;
        public bool ShowConnect => !Sheets.IsBusy;
        public bool ShowConnectedOptions => Sheets.IsConnected && !Sheets.IsBusy;
        public bool ShowQueue => Sheets.PendingCount > 0;
        public bool ShowRetry => Sheets.IsConnected && Sheets.SelectedPendingCount > 0;
        public bool ShowStatusDetail => !Sheets.IsBusy && (Sheets.HasError || Sheets.HasNotice);
        public string ConnectLabel => Sheets.IsConnected ? "Change spreadsheet…" : "Connect Google Sheets…";
        public string SheetsState => Sheets.Activity switch
        {
            GoogleSheetsActivity.Connecting => "Finish in your browser",
            GoogleSheetsActivity.Disconnecting => "Disconnecting…",
            GoogleSheetsActivity.Testing => "Testing write access…",
            GoogleSheetsActivity.Uploading => "Uploading…",
            _ when !Sheets.IsAvailable => "Setup needs attention",
            _ when Sheets.HasError => "Needs attention",
            _ when !Sheets.IsConnected => "Not connected",
            _ => Settings.Instance.LogToSheet ? "Logging on" : "Logging paused"
        };
        public string SheetsStateColor => Sheets.HasError ? "#E8B568" : Sheets.IsBusy ? "#8FC7FF"
            : Sheets.IsConnected && Settings.Instance.LogToSheet ? "#88CFA6" : "#AAA";
        public string SheetsDescription => Sheets.Activity switch
        {
            GoogleSheetsActivity.Connecting => Sheets.IsConnected
                ? "Choose a spreadsheet in the Google tab. Your current selection stays in place unless you finish."
                : "Sign in and choose one spreadsheet in the Google tab. Return here when you’re done, or cancel below.",
            GoogleSheetsActivity.Disconnecting => "Removing saved sign-in details. Your spreadsheet data will not be deleted.",
            GoogleSheetsActivity.Testing => "The test only writes to B2 if it contains no value or formula.",
            GoogleSheetsActivity.Uploading => "You can keep playing. Saved records are being sent to the selected spreadsheet.",
            _ when !Sheets.IsAvailable => "Google Sheets is unavailable. Follow the message below before trying again.",
            _ when !Sheets.IsConnected => "Save completed floor times to a spreadsheet you choose. Google only grants access to files you select.",
            _ when !Settings.Instance.LogToSheet => "New floors are not queued for Google Sheets, and saved uploads are paused. Turn logging on to resume.",
            _ => "Floor times are saved in one column of the first visible worksheet. Only floors with Bonus = 1 are logged."
        };
        public string QueueSummary
        {
            get
            {
                int current = Sheets.SelectedPendingCount;
                int other = Sheets.PendingCount - current;
                if (!Sheets.IsConnected) return $"{other} saved upload(s). Connect to their original spreadsheet to send them.";
                string message = current == 0 ? "" : $"{current} upload(s) waiting for this spreadsheet. " +
                    (Settings.Instance.LogToSheet ? "Retries run automatically every 30 seconds." : "Turn logging on to resume uploads.");
                if (other > 0) message += (message.Length > 0 ? "\n" : "") +
                    $"{other} upload(s) belong to another spreadsheet. Choose that spreadsheet to send them.";
                return message;
            }
        }

        private void RefreshSheetsState(object? sender, PropertyChangedEventArgs e)
        {
            if (sender == Settings.Instance && e.PropertyName is not (nameof(Settings.LogToSheet) or nameof(Settings.SpreadsheetStartCell))) return;
            foreach (string property in new[] { nameof(CanManageSheets), nameof(CanUseSheet), nameof(CanRetrySheets),
                nameof(ShowConnect), nameof(ShowConnectedOptions), nameof(ShowQueue), nameof(ShowRetry), nameof(ShowStatusDetail), nameof(ConnectLabel),
                nameof(SheetsState), nameof(SheetsStateColor), nameof(SheetsDescription), nameof(QueueSummary), nameof(CanApplyStartCell), nameof(DestinationSummary) })
                OnPropertyChanged(property);
            ConnectGoogleSheetsCommand.NotifyCanExecuteChanged();
            DisconnectGoogleSheetsCommand.NotifyCanExecuteChanged();
            WriteSheetsDemoCommand.NotifyCanExecuteChanged();
            RetrySheetsCommand.NotifyCanExecuteChanged();
            ApplySheetsStartCellCommand.NotifyCanExecuteChanged();
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanManageSheets))]
        private Task ConnectGoogleSheetsAsync(CancellationToken cancellationToken) => Sheets.ConnectAsync(cancellationToken);

        [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanUseSheet))]
        private Task DisconnectGoogleSheetsAsync() => Sheets.DisconnectAsync();

        [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanUseSheet))]
        private Task WriteSheetsDemoAsync() => Sheets.WriteDemoAsync();

        [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanRetrySheets))]
        private Task RetrySheetsAsync() => Sheets.RetryPendingAsync();
    }
}
