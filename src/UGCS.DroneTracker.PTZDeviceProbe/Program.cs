using System;
using System.IO;
using System.Net.Mime;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Swan;
using Swan.Parsers;
using UGCS.DroneTracker.Core;
using UGCS.DroneTracker.Core.PelcoD;
using UGCS.DroneTracker.Core.PTZ;
using UGCS.DroneTracker.Core.Settings;

namespace UGCS.DroneTracker.PTZDeviceProbe
{
    

    internal class Options
    {
        [ArgumentOption('t', Required = true, HelpText = "Transport type (udp|serial)", DefaultValue = PTZDeviceTransportType.Udp)]
        public PTZDeviceTransportType TransportType { get; set; }

        [ArgumentOption("serial-name", Required = false,
            HelpText = "PTZ Serial port name (for serial port transport type), default=COM1", DefaultValue = "COM1")]
        public string PTZSerialPortName { get; set; } = "COM1";

        [ArgumentOption("serial-speed", Required = false,
            HelpText = "PTZ Serial port name (for serial port transport type), default=9600", DefaultValue = 9600)]
        public int PTZSerialPortSpeed { get; set; }



        [ArgumentOption('h', "host", Required = false, 
            HelpText = "PTZ UDP host (for udp transport type), default=192.168.0.93", DefaultValue = "192.168.0.93")]
        public string PTZUdpHost { get; set; }

        [ArgumentOption('p', "port", Required = false,
            HelpText = "PTZ UDP port (for udp transport type), default=6000", DefaultValue = 6000)]
        public int PTZUdpPort { get; set; }

        
        [ArgumentOption('a', "address", Required = false, HelpText = "PTZ Device address, default=1", DefaultValue = 1)]
        public byte PTZDeviceAddress { get; set; }

    }

    class Program
    {
        private static readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<Program>();

        private static IPTZDeviceMessagesTransport _transport;
        private static AppSettingsDto _defaultCoreOptions;

        public static IApplicationSettingsManager _settingsManager;
        public static PelcoRequestBuilder _requestBuilder;
        public static PelcoResponseDecoder _responseDecoder;
        private static PelcoDeviceController _controller;
        private static TaskCompletionSource<IPTZMessage> _sendMessageTaskCompletionSource;
        private static double _initialTilt;
        private static double _initialPan;
        private static ushort? _maxPan;
        private static ushort? _maxTilt;
        private static CancellationTokenSource _cts;

        static async Task Main(string[] args)
        {
            setupLogConfig();

            _logger.LogInfoMessage($"App started => args:{args.ToJson()}");

            Terminal.WriteLine("UGCS DroneTracker - PTZ Probe tool", ConsoleColor.Yellow);

            // create a new instance of the class that we want to parse the arguments into
            var options = new Options();

            // if everything went out fine the ParseArguments method will return true
            ArgumentParser.Current.ParseArguments(args, options);
            _logger.LogInfoMessage($"Options parsed => options:\n{options.ToJson()}");

            _transport = options.TransportType == PTZDeviceTransportType.Udp
                ? (IPTZDeviceMessagesTransport) new UdpPTZMessagesTransport(options.PTZUdpHost, options.PTZUdpPort)
                : new SerialPortPTZMessagesTransport(options.PTZSerialPortName, options.PTZSerialPortSpeed);

            logInfo("Initialize transport...");
            try
            {
                // TODO
                // await _transport.Initialize();
                _transport.Initialize();
                logOk("Transport initialized");
            }
            catch (Exception e)
            {
                _logger.LogException(e);
                Terminal.WriteLine("Transport initialize error! Check logs to details.", ConsoleColor.Red);

                Terminal.WriteLine("Press enter to exit...");
                Terminal.ReadLine();

                Environment.Exit(1);
            }

            logInfo("Create PTZ device controller");

            _settingsManager = new ApplicationSettingsManager(null);

            _defaultCoreOptions = _settingsManager.GetAppSettings();
            
            _requestBuilder = new PelcoRequestBuilder(_defaultCoreOptions.PelcoCodesMapping);
            _responseDecoder = new PelcoResponseDecoder(_defaultCoreOptions.PelcoCodesMapping);

            _controller = new PelcoDeviceController(_transport, _settingsManager, _requestBuilder, _responseDecoder);

            logInfo("PTZ device controller created");

            await requestCurrentPan(options);
            
            await requestCurrentTilt(options);
            
            _transport.MessageSending += _transport_MessageSending;
            _transport.MessageReceived += _transport_MessageReceived;

            await requestMaxPan(options);

            await requestMaxTilt(options);

            logInfo("Try to set pan by 0x71 opCode");
            if (_maxPan.HasValue)
            {
                await requestSetPan(options, (ushort)(_maxPan.Value / 2));
                await requestSetPan(options, (ushort)(_maxPan.Value));
                await requestSetPan(options, 0);
                await requestSetPan(options, (ushort)(_initialPan * 100));
            }
            else
            {
                await requestSetPan(options, 9000);
                await requestSetPan(options, 18000);
                await requestSetPan(options, 0);
                await requestSetPan(options, (ushort)(_initialPan * 100));
            }

            logInfo("Try to set pan by 0x4B opCode");
            byte pelcoSetPanCode = 0x4b;
            if (_maxPan.HasValue)
            {
                await requestSetPan(options, (ushort)(_maxPan.Value / 2), pelcoSetPanCode);
                await requestSetPan(options, (ushort)(_maxPan.Value), pelcoSetPanCode);
                await requestSetPan(options, 0, pelcoSetPanCode);
                await requestSetPan(options, (ushort)(_initialPan * 100), pelcoSetPanCode);
            }
            else
            {
                await requestSetPan(options, 9000, pelcoSetPanCode);
                await requestSetPan(options, 0, pelcoSetPanCode);
                await requestSetPan(options, (ushort)(_initialPan * 100), pelcoSetPanCode);
            }


            logInfo("Try to set tilt by 0x73 opCode");
            if (_maxTilt.HasValue)
            {
                await requestSetTilt(options, (ushort)(_maxTilt.Value / 2));
                await requestSetTilt(options, (ushort)(_maxTilt.Value));
                await requestSetTilt(options, 0);
                await requestSetTilt(options, (ushort) (_initialTilt * 100));
            }
            else
            {
                await requestSetTilt(options, 4500);
                await requestSetTilt(options, 0);
                await requestSetTilt(options, (ushort) (_initialTilt * 100));
            }

            byte pelcoSetTiltCode = 0x4d;
            logInfo("Try to set tilt by 0x4D opCode");
            if (_maxTilt.HasValue)
            {
                await requestSetTilt(options, (ushort)(_maxTilt.Value / 2), pelcoSetTiltCode);
                await requestSetTilt(options, (ushort)(_maxTilt.Value), pelcoSetTiltCode);
                await requestSetTilt(options, 0, pelcoSetTiltCode);
                await requestSetTilt(options, (ushort)(_initialTilt * 100), pelcoSetTiltCode);
            }
            else
            {
                await requestSetTilt(options, 4500, pelcoSetTiltCode);
                await requestSetTilt(options, 0, pelcoSetTiltCode);
                await requestSetTilt(options, (ushort)(_initialPan * 100), pelcoSetTiltCode);
            }


            _logger.LogInfoMessage($"Done. Waiting user to exit.");

            Terminal.WriteLine();
            Terminal.WriteLine("Done.", ConsoleColor.Yellow);
            Terminal.WriteLine("Press enter to exit...", ConsoleColor.Yellow);

            _transport.Teardown();

            Terminal.ReadLine();
        }


        private static void logInfo(string message)
        {
            Terminal.WriteLine(message);
            _logger.LogInfoMessage(message);
        }

        private static void logOk(string message)
        {
            Terminal.WriteLine(message, ConsoleColor.Green);
            _logger.LogInfoMessage(message);
        }


        private static void logError(string message)
        {
            Terminal.WriteLine(message, ConsoleColor.Red);
            _logger.LogError(message);
        }


        private static async Task requestSetPan(Options options, ushort panCoordinate, byte opCode = 0x71)
        {
            logInfo($"Try to request set pan to {panCoordinate}...");

            var coordinateBytes = BitConverter.GetBytes(panCoordinate);

            var requestSetPanMessage = new PelcoDEMessage(options.PTZDeviceAddress, opCode, coordinateBytes[1], coordinateBytes[0]);
            try
            {
                var responseMessage = await SendMessage(requestSetPanMessage);
                if (responseMessage != null)
                {
                    logOk($"Request set pan done");
                }
                else
                {
                    logError("Set pan request got null response message");
                }
            }
            catch (TaskCanceledException)
            {
                logError("Set pan request cancelled by timeout");
            }
            catch (Exception e)
            {
                Terminal.WriteLine("Set pan request! Check logs to details.", ConsoleColor.Red);
                _logger.LogException(e);
            }
        }

        private static async Task requestSetTilt(Options options, ushort tiltCoordinate, byte opCode = 0x73)
        {
            logInfo($"Try to request set tilt to {tiltCoordinate}...");

            var coordinateBytes = BitConverter.GetBytes(tiltCoordinate);

            var requestSetPanMessage = new PelcoDEMessage(options.PTZDeviceAddress, opCode, coordinateBytes[1], coordinateBytes[0]);
            try
            {
                var responseMessage = await SendMessage(requestSetPanMessage);
                if (responseMessage != null)
                {
                    logOk($"Request set tilt done");
                }
                else
                {
                    logError("Set tilt request got null response message");
                }
            }
            catch (TaskCanceledException)
            {
                logError("Set tilt request cancelled by timeout");
            }
            catch (Exception e)
            {
                Terminal.WriteLine("Set tilt request error! Check logs to details.", ConsoleColor.Red);
                _logger.LogException(e);
            }
        }

        private static async Task requestMaxTilt(Options options)
        {
            logInfo("Try to request max tilt...");

            var requestMaxTiltMessage = new PelcoDEMessage(options.PTZDeviceAddress, 0x57);
            try
            {
                var responseMessage = await SendMessage(requestMaxTiltMessage);
                if (responseMessage != null)
                {
                    _logger.LogInfoMessage($"Request max tilt done, message={responseMessage}");
                    var maxTilt = PelcoResponseDecoder.GetUInt16((PelcoDEMessage) responseMessage);
                    _maxTilt = maxTilt;

                    logOk($"Request max tilt done, Max Tilt={maxTilt}");
                }
                else
                {
                    logError($"Request max tilt got null response message");
                }
            }
            catch (TaskCanceledException)
            {
                logError("Max tilt request cancelled by timeout");
            }
            catch (Exception e)
            {
                Terminal.WriteLine("Max tilt request! Check logs to details.", ConsoleColor.Red);
                _logger.LogException(e);
            }
        }

        private static async Task requestMaxPan(Options options)
        {
            logInfo("Try to request max pan...");

            var requestMaxPanMessage = new PelcoDEMessage(options.PTZDeviceAddress, 0x55);
            try
            {
                var responseMessage = await SendMessage(requestMaxPanMessage);
                if (responseMessage != null)
                {
                    _logger.LogInfoMessage($"Request max pan done, message={responseMessage}");
                    var maxPan = PelcoResponseDecoder.GetUInt16((PelcoDEMessage) responseMessage);
                    _maxPan = maxPan;

                    logOk($"Request max pan done, Max Pan={maxPan}");
                }
                else
                {
                    logError($"Request max pan got null response message");
                }
            }
            catch (TaskCanceledException)
            {
                logError("Max pan request cancelled by timeout");
            }
            catch (Exception e)
            {
                Terminal.WriteLine("Max pan request! Check logs to details.", ConsoleColor.Red);
                _logger.LogException(e);
            }
        }

        private static async Task requestCurrentTilt(Options options)
        {
            logInfo("Try to request current tilt...");
            try
            {
                var currentTilt = await _controller.RequestTiltAngleAsync(options.PTZDeviceAddress);
                _initialTilt = currentTilt;
                logOk($"Request current tilt done, tilt={currentTilt}");
            }
            catch (TaskCanceledException)
            {
                logError("Current tilt request cancelled by timeout");
            }
            catch (Exception e)
            {
                Terminal.WriteLine("Current pan request! Check logs to details.", ConsoleColor.Red);
                _logger.LogException(e);
            }
        }

        private static async Task requestCurrentPan(Options options)
        {
            logInfo("Try to request current pan...");
            try
            {
                var currentPan = await _controller.RequestPanAngleAsync(options.PTZDeviceAddress);
                _initialPan = currentPan;
                logOk($"Request current pan done, Current Pan={currentPan}");
            }
            catch (TaskCanceledException)
            {
                logError("Current pan request cancelled by timeout");
            }
            catch (Exception e)
            {
                Terminal.WriteLine("Current pan request error! Check logs to details.", ConsoleColor.Red);
                _logger.LogException(e);
            }
        }

        private static void _transport_MessageReceived(object sender, byte[] e)
        {
            var pelcoMessage = PelcoDEMessage.FromBytes(e);
            _logger.LogInfoMessage($"Pelco message received => {pelcoMessage?.ToString() ?? "<null>"}");
            if (pelcoMessage == null) return;

            _cts.Dispose();
            var trySetResult = _sendMessageTaskCompletionSource?.TrySetResult(pelcoMessage);
            _logger.LogInfoMessage($"Set async task result: {trySetResult}");
        }

        private static void _transport_MessageSending(object sender, byte[] e)
        {
            //_logger.LogDebugMessage($"transport_MessageSending => bytes");
        }

        public static Task<IPTZMessage> SendMessage(IPTZMessage message)
        {
            _logger.LogDebugMessage($"SendMessage => {message}");

            _sendMessageTaskCompletionSource = new TaskCompletionSource<IPTZMessage>();

            _cts = new CancellationTokenSource(new TimeSpan(0, 0, 15));

            _cts.Token.Register(() => _sendMessageTaskCompletionSource?.TrySetCanceled());

            _transport.SendMessage(message);

            return _sendMessageTaskCompletionSource.Task;
        }


        private static void setupLogConfig()
        {
            var log4NetConfig = new XmlDocument();
            log4NetConfig.Load(File.OpenRead("log4net.config"));
            var repo = LogManager.CreateRepository(Assembly.GetEntryAssembly(), typeof(Hierarchy));
            XmlConfigurator.Configure(repo, log4NetConfig["log4net"]);
        }

    }


}
