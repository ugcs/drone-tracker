using UGCS.DroneTracker.Core.Annotations;

namespace UGCS.DroneTracker.Core.Settings
{
    public interface IApplicationSettingsManager<T> where T : class
    {
        T GetAppSettings();
        void Save(T settingsDto = null);
    }

    public interface IApplicationSettingsManager : IApplicationSettingsManager<AppSettingsDto>
    {
    }

    public class ApplicationSettingsManager : IApplicationSettingsManager
    {
        private readonly IApplicationSettingsStorage<AppSettingsDto> _storage;
        private readonly AppSettingsDto _settingsDto;

        public ApplicationSettingsManager(IApplicationSettingsStorage<AppSettingsDto> applicationSettingsStorage)
        {
            _storage = applicationSettingsStorage;
            _settingsDto = _storage?.LoadSettings() ?? new AppSettingsDto();
        }

        public AppSettingsDto GetAppSettings() => _settingsDto;

        public void Save([CanBeNull] AppSettingsDto settingsDto = null)
        {
            _storage?.SaveSettings(settingsDto ?? _settingsDto);
        }
    }
}