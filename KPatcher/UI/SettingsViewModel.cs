using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using KPatcher.Properties;

namespace KPatcher.UI
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _restartRequired;
        private bool _unsavedChanges;
        private bool _enableConsole;
        private bool _blockNetwork;
        private bool _offlineMode;
        private LogLevel _debugLevel;
        private bool _patchLicense;
        private bool _enableUpdates;
        private bool _passUnknownRequests;

        public bool EnableConsole
        {
            get => _enableConsole;
            set
            {
                if (_enableConsole == value) return;
                _enableConsole = value;
                OnPropertyChanged(nameof(EnableConsole));
            }
        }

        //Since I don't want to use any massive converters or calculation libs for XAML
        //XAML condition property
        public bool AllowNetwork => !BlockNetwork;

        public bool BlockNetwork
        {
            get => _blockNetwork;
            set
            {
                if (_blockNetwork == value) return;
                _blockNetwork = value;
                _restartRequired = true;
                OnPropertyChanged(nameof(BlockNetwork));
                OnPropertyChanged(nameof(AllowNetwork));
                OnPropertyChanged(nameof(NotOfflineMode));
            }
        }

        //XAML condition property
        public bool NotOfflineMode => !OfflineMode && !BlockNetwork;

        public bool OfflineMode
        {
            get => _offlineMode;
            set
            {
                if (_offlineMode == value) return;
                _offlineMode = value;
                _restartRequired = true;
                OnPropertyChanged(nameof(OfflineMode));
                OnPropertyChanged(nameof(NotOfflineMode));
            }
        }

        public bool PatchLicense
        {
            get => _patchLicense;
            set
            {
                if (_patchLicense == value) return;
                _patchLicense = value;
                _restartRequired = true;
                OnPropertyChanged(nameof(PatchLicense));
            }
        }

        public bool EnableUpdates
        {
            get => _enableUpdates;
            set
            {
                if (_enableUpdates == value) return;
                _enableUpdates = value;
                _restartRequired = true;
                OnPropertyChanged(nameof(EnableUpdates));
            }
        }

        public bool PassUnknownRequests
        {
            get => _passUnknownRequests;
            set
            {
                if (_passUnknownRequests == value) return;
                _passUnknownRequests = value;
                OnPropertyChanged(nameof(PassUnknownRequests));
            }
        }

        public LogLevel DebugLevel
        {
            get => _debugLevel;
            set
            {
                if (_debugLevel == value) return;
                _debugLevel = value;
                OnPropertyChanged(nameof(DebugLevel));
            }
        }

        public SettingsViewModel()
        {
            _enableConsole = Settings.Default.ShowConsole;
            _blockNetwork = Settings.Default.BlockNetwork;
            _offlineMode = Settings.Default.OfflineMode;
            _debugLevel = (LogLevel)Settings.Default.DebugLevel;
            _patchLicense = Settings.Default.PatchLicense;
            _enableUpdates = Settings.Default.EnableUpdates;
            _passUnknownRequests = Settings.Default.PassUnknownRequests;
        }

        public void Save()
        {
            if (_restartRequired)
            {
                var result = Utils.ShowDialog("Are you sure?",
                    "The app will be restarted to apply new changes.\r\nDo you still want to proceed?", "No", "Yes");
                if (result == MessageBoxResult.Cancel)
                    return;
            }

            Settings.Default.ShowConsole = EnableConsole;
            Settings.Default.BlockNetwork = BlockNetwork;
            Settings.Default.OfflineMode = OfflineMode;
            Settings.Default.PatchLicense = PatchLicense;
            Settings.Default.EnableUpdates = EnableUpdates;
            Settings.Default.DebugLevel = (int)DebugLevel;
            Settings.Default.PassUnknownRequests = PassUnknownRequests;

            try
            {
                Settings.Default.Save();
                _unsavedChanges = false;
                Console.WriteLine("Saved settings");
            }
            catch (Exception e)
            {
                Utils.ShowMessageBox("Could not save settings\r\n" + e.Message);
                return;
            }

            if (!_restartRequired)
            {
                Utils.ShowConsole(Settings.Default.ShowConsole);
                return;
            }
            System.Windows.Forms.Application.Restart();
            Application.Current.Shutdown();
        }

        public void ResetSettings()
        {
            var result = Utils.ShowDialog("Are you sure?",
                    "The app will be restarted to apply default settings.\r\nDo you still want to proceed?", "No", "Yes");
            if (result == MessageBoxResult.Cancel)
                return;

            Settings.Default.Reset();

            try
            {
                Settings.Default.Save();
                Console.WriteLine("Saved settings");
            }
            catch (Exception e)
            {
                Utils.ShowMessageBox("Could not save settings\r\n" + e.Message);
                return;
            }

            System.Windows.Forms.Application.Restart();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Processes window closing event
        /// </summary>
        /// <returns>True when it's okay to proceed closing window</returns>
        public bool ProcessClosing()
        {
            if (!_unsavedChanges) return true;
            var result = Utils.ShowDialog("Are you sure?",
                "You have unsaved changes.\r\nDo you still want to close the window?", "No", "Yes");
            return result == MessageBoxResult.OK;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            _unsavedChanges = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}