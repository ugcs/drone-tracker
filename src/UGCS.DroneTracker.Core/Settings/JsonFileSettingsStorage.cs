using System;
using System.IO;
using Newtonsoft.Json;

namespace UGCS.DroneTracker.Core.Settings
{
    public interface IApplicationSettingsStorage<T> where T : class
    {
        public T LoadSettings();
        public void SaveSettings(T settings);
    }


    public class JsonFileSettingsStorage<T> : IApplicationSettingsStorage<T> where T: class
    {
        private readonly string _filePath;

        public JsonFileSettingsStorage(string directory, string fileName)
        {
            _filePath = getLocalFilePath(directory, fileName);
        }

        private string getLocalFilePath(string directory, string fileName)
        {
            var appDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataFolderPath, directory, fileName);
        }

        public T LoadSettings() =>
            File.Exists(_filePath) ?
                JsonConvert.DeserializeObject<T>(File.ReadAllText(_filePath)) :
                null;

        public void SaveSettings(T settings)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
    }
}