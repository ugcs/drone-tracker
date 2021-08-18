using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UGCS.DroneTracker.Core.PelcoD;
using UGCS.DroneTracker.Core.Settings;

namespace UGCS.DroneTracker.Core.PTZ
{
    public interface IPTZDeviceController
    {
        void Pan(byte address, PanDirection direction, byte speed);
        void Tilt(byte address, TiltDirection direction, byte speedPercent);

        void PanTo(byte address, double angle);
        Task<bool> PanToAsync(byte address, double angle);

        void TiltTo(byte address, double angle);
        Task<bool> TiltToAsync(byte address, double angle);

        void Stop(byte address);
    }

    public interface IQueryablePTZDeviceController : IPTZDeviceController
    {
        void RequestPanAngle(byte address);
        Task<double> RequestPanAngleAsync(byte address);
        
        void RequestTiltAngle(byte address);
        Task<double> RequestTiltAngleAsync(byte address);
    }


    public class PelcoDeviceController : IQueryablePTZDeviceController
    {
        private const int REQUEST_ANGLES_DEFAULT_TIMEOUT = 3000;
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<PelcoDeviceController>();

        private readonly IPTZDeviceMessagesTransport _transport;
        private TaskCompletionSource<bool> _panTaskCompletionSource;
        private TaskCompletionSource<double> _requestPanTaskCompletionSource;
        private readonly IApplicationSettingsManager _settingsManager;
        private readonly PelcoRequestBuilder _requestBuilder;
        private readonly PelcoResponseDecoder _responseDecoder;
        private TaskCompletionSource<double> _requestTiltTaskCompletionSource;
        private CancellationTokenSource _panRequestCTS;
        private CancellationTokenSource _tiltRequestCTS;

        public PelcoDeviceController(
            IPTZDeviceMessagesTransport transport, 
            IApplicationSettingsManager settingsManager,
            PelcoRequestBuilder requestBuilder,
            PelcoResponseDecoder responseDecoder)
        {
            _transport = transport;
            _transport.MessageReceived += transport_MessageReceived;

            _settingsManager = settingsManager;
            _requestBuilder = requestBuilder;
            _responseDecoder = responseDecoder;

            //GetPtzDeviceTiltCoordinatesImpl = PelcoDeviceController.DefaultGetPtzDeviceTiltCoordinatesImpl;
            GetPtzDeviceTiltCoordinatesImpl = PelcoDeviceController.HDS3051GetPtzDeviceTiltCoordinatesImpl;
        }

        private void transport_MessageReceived(object sender, byte[] messageData)
        {
            var pelcoMessage = PelcoDEMessage.FromBytes(messageData);
            _logger.LogInfoMessage($"Pelco message received => {pelcoMessage?.ToString() ?? "<null>"}");
            if (pelcoMessage == null) return;

            var settings = _settingsManager.GetAppSettings();

            var responseType = _responseDecoder.GetResponseType(pelcoMessage);
            _logger.LogInfoMessage($"Pelco response type => {responseType?.ToString() ?? "<null>"}");

            switch (responseType)
            {
                case null:
                    return;
                case PelcoDEMessageType.SetPanCompleteResponse:
                    _panTaskCompletionSource?.TrySetResult(true);
                    return;
                case PelcoDEMessageType.RequestPanResponse:
                {
                    var panAngleToCoordinatesFactor = settings.PTZPanAngleToCoordinateFactor;
                    var angle = PelcoResponseDecoder.GetUInt16(pelcoMessage) / panAngleToCoordinatesFactor / 100;
                    _logger.LogDebugMessage($"Pelco RequestPanResponse received => angle: {angle}");
                    _panRequestCTS.Dispose();
                    _requestPanTaskCompletionSource?.TrySetResult(angle);
                    return;
                }
                case PelcoDEMessageType.RequestTiltResponse:
                {
                    var tiltAngleToCoordinatesFactor = settings.PTZTiltAngleToCoordinateFactor;
                    var angle = PelcoResponseDecoder.GetUInt16(pelcoMessage) / tiltAngleToCoordinatesFactor / 100;
                    _logger.LogDebugMessage($"Pelco RequestTiltResponse received => angle: {angle}");
                    _tiltRequestCTS.Dispose();
                    _requestTiltTaskCompletionSource?.TrySetResult(angle);
                    return;
                }
            }
        }

        private byte getMovementSpeedValue(int speedPercent)
        {
            var settings = _settingsManager.GetAppSettings();
            return (byte)(settings.PTZMaxSpeed * Math.Min(speedPercent, 100) / 100);
        }

        public void Pan(byte address, PanDirection direction, byte speedPercent)
        {
            var message = _requestBuilder.Pan(address, direction, getMovementSpeedValue(speedPercent));
            _logger.LogDebugMessage($"Pan {direction} / {speedPercent} => {message}");
            _transport.SendMessage(message);
        }

        public Task<bool> PanToAsync(byte address, double angle)
        {
            var settings = _settingsManager.GetAppSettings();
            var panAngleToCoordinatesFactor = settings.PTZPanAngleToCoordinateFactor;
            _panTaskCompletionSource = new TaskCompletionSource<bool>();

            var message = _requestBuilder.SetPan(address, (int)(angle * panAngleToCoordinatesFactor * 100d));
            _logger.LogDebugMessage($"PanTo async {angle} * {panAngleToCoordinatesFactor} => {message}");
            _transport.SendMessage(message);

            return _panTaskCompletionSource.Task;
        }

        public void Tilt(byte address, TiltDirection direction, byte speedPercent)
        {
            var message = _requestBuilder.Tilt(address, direction, getMovementSpeedValue(speedPercent));
            _logger.LogDebugMessage($"Tilt {direction} @ {speedPercent} speed => {message}");
            _transport.SendMessage(message);
        }

        public void PanTo(byte address, double angle)
        {
            var settings = _settingsManager.GetAppSettings();
            var panAngleToCoordinatesFactor = settings.PTZPanAngleToCoordinateFactor;
            var message = _requestBuilder.SetPan(address, (int)(angle * panAngleToCoordinatesFactor * 100d));
            _logger.LogDebugMessage($"PanTo {angle} * {panAngleToCoordinatesFactor} => {message}");
            _transport.SendMessage(message);
        }

        public Func<double, double> GetPtzDeviceTiltCoordinatesImpl { get; set; }

        private static double DefaultGetPtzDeviceTiltCoordinatesImpl(double angle)
        {
            return angle;
        }

        private static double HDS3051GetPtzDeviceTiltCoordinatesImpl(double angle)
        {
            if (angle <= 0)
            {
                return Math.Abs(angle);
            }
            else
            {
                return 360d - angle;
            }
        }

        public void TiltTo(byte address, double angle)
        {
            var settings = _settingsManager.GetAppSettings();
            var tiltAngleToCoordinatesFactor = settings.PTZTiltAngleToCoordinateFactor;

            var ptzTiltCoordinates =
                (GetPtzDeviceTiltCoordinatesImpl?.Invoke(angle) ?? 0) * tiltAngleToCoordinatesFactor;

            var message = _requestBuilder.SetTilt(address, (int)(ptzTiltCoordinates * 100d));
            _logger.LogDebugMessage($"TiltTo {angle} * {tiltAngleToCoordinatesFactor} => {message}");
            _transport.SendMessage(message);
        }

        public Task<bool> TiltToAsync(byte address, double angle)
        {
            throw new NotImplementedException();
        }

        public void Stop(byte address)
        {
            var message = _requestBuilder.Stop(address);
            _logger.LogDebugMessage($"Stop => {message}");
            _transport.SendMessage(message);
        }

        public void RequestPanAngle(byte address)
        {
            var message = _requestBuilder.RequestPan(address);
            _logger.LogDebugMessage($"RequestPanAngle => {message}");
            _transport.SendMessage(message);
        }

        public void RequestTiltAngle(byte address)
        {
            var message = _requestBuilder.RequestTilt(address);
            _logger.LogDebugMessage($"RequestTiltAngle => {message}");
            _transport.SendMessage(message);
        }

        public Task<double> RequestPanAngleAsync(byte address)
        {
            _requestPanTaskCompletionSource = new TaskCompletionSource<double>();

            _panRequestCTS = new CancellationTokenSource(REQUEST_ANGLES_DEFAULT_TIMEOUT);

            _panRequestCTS.Token.Register(() => _requestPanTaskCompletionSource?.TrySetCanceled());

            var message = _requestBuilder.RequestPan(address);
            _logger.LogDebugMessage($"RequestPanAngleAsync => {message}");
            _transport.SendMessage(message);

            return _requestPanTaskCompletionSource.Task;
        }

        public Task<double> RequestTiltAngleAsync(byte address)
        {
            _requestTiltTaskCompletionSource = new TaskCompletionSource<double>();

            _tiltRequestCTS = new CancellationTokenSource(REQUEST_ANGLES_DEFAULT_TIMEOUT);
            _tiltRequestCTS.Token.Register(() => _requestTiltTaskCompletionSource?.TrySetCanceled());

            var message = _requestBuilder.RequestTilt(address);
            _logger.LogDebugMessage($"RequestTiltAngleAsync => {message}");
            _transport.SendMessage(message);

            return _requestTiltTaskCompletionSource.Task;
        }

    }
}