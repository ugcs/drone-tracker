using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UGCS.Sdk.Protocol;
using UGCS.Sdk.Protocol.Encoding;

namespace UGCS.DroneTracker.Core.UGCS
{
    public class UGCSFacade
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<UGCSFacade>();

        private const string SELECTION_REGEX_PATTERN = @"<selection +selection=\""(\d*)\"" *\/>";

        private readonly UGCSConnection _connection;

        public UGCSFacade(UGCSConnection connection)
        {
            _connection = connection;
        }

        public async Task<List<Vehicle>> GetVehiclesAsync()
        {
            _logger.LogInfoMessage("GetVehiclesAsync requested");

            var objRequestTask = Task<List<DomainObjectWrapper>>.Factory.StartNew(() => _connection.GetObjectList<Vehicle>());
            try
            {
                var responseObjects = await objRequestTask;
                _logger.LogInfoMessage($"GetVehiclesAsync got {responseObjects.Count} response objects");
                return responseObjects.Select(x => x.Vehicle).ToList();
            }
            catch (Exception e)
            {
                _logger.LogException(e);
                throw;
            }
        }


        private void subscribe<T>(NotificationHandler action) where T : IIdentifiable
        {
            _connection.Subscribe<T>(action);
        }

        public void SubscribeToVehicleModification(Action<Vehicle, ModificationType> handler)
        {
            subscribe<Vehicle>(notification =>
            {
                var objModificationEvent = notification.Event.ObjectModificationEvent;
                var modType = objModificationEvent.ModificationType;
                var vehicle = objModificationEvent.Object?.Vehicle;
                handler?.Invoke(vehicle, modType);
            });
        }

        public void SubscribeToMissionPreferences(Action<int?> notificationHandler)
        {
            subscribe<MissionPreference>((notification) =>
            {
                var updatedPreference = notification?.Event?.ObjectModificationEvent?.Object?.MissionPreference;
                if (updatedPreference == null || !isUserMission(updatedPreference)) return;

                var selectedVehicleId = getSelectedVehicleIdFromMissionPref(updatedPreference);

                notificationHandler?.Invoke(selectedVehicleId);
            });
        }

        public void SubscribeToTelemetry(Action<int, List<Telemetry>> telemetryNotificationHandler)
        {
            _connection.SubscribeToTelemetry(notification =>
            {
                var telemetryEvent = notification.Event.TelemetryEvent;
                var vehicleId = telemetryEvent.Vehicle.Id;
                var telemetry = telemetryEvent.Telemetry;

                telemetryNotificationHandler?.Invoke(vehicleId, telemetry);
            });
        }


        private bool isUserMission(MissionPreference preference)
        {
            return preference.Name.Equals("mission") && preference.User.Id == _connection.LastLoginResponse.User.Id;
        }

        private int? getSelectedVehicleIdFromMissionPref(MissionPreference preference)
        {
            int? result = null;
            var r = new Regex(SELECTION_REGEX_PATTERN, RegexOptions.IgnoreCase);

            var selectionValue = preference?.Value;

            if (string.IsNullOrWhiteSpace(selectionValue)) return null;

            var match = r.Match(selectionValue);
            if (!match.Success) return null;

            var strId = match.Groups[1].Captures[0].Value;
            if (int.TryParse(strId, out var droneId))
            {
                result = droneId;
            }

            return result;
        }

        public async Task<int?> GetSelectedVehicleIdAsync()
        {
            _logger.LogInfoMessage("GetSelectedVehicleIdAsync requested");

            int? result = null;
            var r = new Regex(SELECTION_REGEX_PATTERN, RegexOptions.IgnoreCase);

            var missionPreferencesRequestTask =
                Task<List<MissionPreference>>.Factory.StartNew(() => _connection.GetMissionPreferences(null));

            try
            {
                var missionPreferences = await missionPreferencesRequestTask;

                _logger.LogDebugMessage($"GetSelectedVehicleIdAsync response: {missionPreferences}");
                if (missionPreferences == null) return null;

                _logger.LogDebugMessage($"GetSelectedVehicleIdAsync response: {missionPreferences.Count} missionPreferences");

                foreach (var missionPreference in missionPreferences)
                {
                    if (!isUserMission(missionPreference)) continue;

                    var selectedVehicleInMissionPref = getSelectedVehicleIdFromMissionPref(missionPreference);
                    if (selectedVehicleInMissionPref.HasValue)
                    {
                        result = selectedVehicleInMissionPref;
                    }

                    break;
                }
                _logger.LogInfoMessage($"GetSelectedVehicleIdAsync result: {result}");

                return result;
            }
            catch (Exception e)
            {
                _logger.LogException(e);
                throw;
            }
        }
    }
}