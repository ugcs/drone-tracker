using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UGCS.DroneTracker.Core.Annotations;
using UGCS.Sdk.Protocol;
using UGCS.Sdk.Protocol.Encoding;
using UGCS.Sdk.Tasks;

namespace UGCS.DroneTracker.Core.UGCS
{
    public class UGCSConnectionResult
    {
        public string Message { get; set; }
        public int Status { get; set; }
    }

    public enum UgcsConnectionStatus
    {
        NotConnected = 0,
        Connecting = 1,
        Connected = 2
    }



    public class UGCSConnection : INotifyPropertyChanged
    {
        private readonly IApplicationLogger _logger = DefaultApplicationLogger.GetLogger<UGCSConnection>();

        private const int CLIENT_LISTENER_ID = -1;
        private const int REGULAR_CONNECT_DELAY_MS = 1000;
        private const int AUTO_CONNECT_DELAY_MS = 2000;


        private readonly MessageExecutor _executor;
        private readonly TcpClient _tcpClient;
        private MessageSender _messageSender;
        private MessageReceiver _messageReceiver;
        private readonly NotificationListener _notificationListener;

        private UgcsConnectionStatus _connectionStatus;

        private AuthorizeHciResponse _authorizeHciResponse;

        public LoginResponse LastLoginResponse { get; private set; }

        public UgcsConnectionStatus ConnectionStatus
        {
            get => _connectionStatus;
            private set
            {
                _connectionStatus = value; 
                OnPropertyChanged();
                onConnectionStatusChanged();
            }
        }

        public event EventHandler<UgcsConnectionStatus> ConnectionStatusChanged;


        public UGCSConnection()
        {
            _executor = new MessageExecutor();
            _executor.Configuration.DefaultTimeout = 10 * 1000;

            _tcpClient = new TcpClient();
            
            _notificationListener = new NotificationListener();

            ConnectionStatus = UgcsConnectionStatus.NotConnected;
        }

        private void tcpClientSessionDisconnected(object? sender, EventArgs e)
        {
            _logger.LogInfoMessage($"TcpClientSessionDisconnected: current ConnectionStatus={ConnectionStatus}");
            CloseConnection();
        }

        private void onConnectionStatusChanged()
        {
            _logger.LogDebugMessage($"onConnectionStatusChanged: current ConnectionStatus={ConnectionStatus}");
            ConnectionStatusChanged?.Invoke(this, _connectionStatus);
        }


        public async Task ConnectAsync(string server, int port, string login, string password)
        {
            _logger.LogInfoMessage($"ConnectAsync: {login}@{server}:{port}");
            ConnectionStatus = UgcsConnectionStatus.Connecting;

            var task = Task<UGCSConnectionResult>.Factory.StartNew(() => connectUgcsInternal(server, port, login, password));

            try
            {
                _logger.LogInfoMessage($"ConnectAsync: Connecting...");
                var result = await task;
                _logger.LogInfoMessage($"Connecting result => {result.Status}:{result.Message}");
                ConnectionStatus = result.Status == 200 ? UgcsConnectionStatus.Connected : UgcsConnectionStatus.NotConnected;
            }
            catch (Exception e)
            {
                _logger.LogException(e);
                ConnectionStatus = UgcsConnectionStatus.NotConnected;
                throw;
            }
        }


        public void CloseConnection()
        {
            _logger.LogInfoMessage("Closing connection");
            ConnectionStatus = UgcsConnectionStatus.NotConnected;

            if (_messageReceiver != null)
            {
                _messageReceiver.Cancel();
                _messageReceiver.RemoveListener(-1);
            }

            _messageSender?.Cancel();
            if (_tcpClient?.Session != null)
            {
                _tcpClient.Session.Disconnected -= tcpClientSessionDisconnected;
            }

            _notificationListener?.Dispose();
            _tcpClient?.Dispose();
        }


        private UGCSConnectionResult connectUgcsInternal(string server, int port, string login, string password)
        {
            try
            {
                _tcpClient.Connect(server, port);
                _tcpClient.Session.Disconnected += tcpClientSessionDisconnected;
            }
            catch (Exception e)
            {
                return new UGCSConnectionResult() { Message = e.Message, Status = 600 };
            }

            _logger.LogInfoMessage($"TCP Client connected");

            _messageSender = new MessageSender(_tcpClient.Session);
            
            _messageReceiver = new MessageReceiver(_tcpClient.Session);

            _executor.SetMessageExecutor(_messageSender, _messageReceiver, new InstantTaskScheduler());
            _messageReceiver.AddListener(CLIENT_LISTENER_ID, _notificationListener);

            var loginRequest = new LoginRequest {UserLogin = login, UserPassword = password};

            var authHciRequest = new AuthorizeHciRequest {ClientId = -1};

            var futureAuth = _executor.Submit<AuthorizeHciResponse>(authHciRequest);

            futureAuth.Wait();

            if (futureAuth.Exception != null)
            {
                return futureAuth.Exception.Message.Contains("Remote connections are not supported") ? 
                    new UGCSConnectionResult() { Message = futureAuth.Exception.Message, Status = 700 } : 
                    new UGCSConnectionResult() { Message = futureAuth.Exception.Message, Status = 400 };
            }
            _authorizeHciResponse = futureAuth.Value;

            loginRequest.ClientId = _authorizeHciResponse.ClientId;

            _logger.LogInfoMessage($"AuthorizeHciRequest - OK");

            var futureLogin = _executor.Submit<LoginResponse>(loginRequest);
            futureLogin.Wait();

            if (futureLogin.Value?.User == null)
            {
                if (LicenseHelper.IsLicenseError(futureLogin.Exception))
                {
                    const string DEFAULT_VERSION = "your license type";
                    var version = LicenseHelper.GetVersionName(futureLogin.Exception.Message);
                    version = string.IsNullOrWhiteSpace(version) ? DEFAULT_VERSION : $"version {version}";
                    return new UGCSConnectionResult() { Message = $"Session number exceeds the maximum allowed for {version}", Status = 500 };
                }
                else
                {
                    return new UGCSConnectionResult() { Message = "Invalid login or password", Status = 300 };
                }
            }
            else if (futureLogin.Exception != null)
            {
                return new UGCSConnectionResult() { Message = futureLogin.Exception.Message, Status = 500 };
            }
            LastLoginResponse = futureLogin.Value;
            _logger.LogInfoMessage($"LoginRequest - OK");

            return new UGCSConnectionResult() { Message = "OK", Status = 200 };
        }


        public List<DomainObjectWrapper> GetObjectList<T>() where T : IIdentifiable
        {
            var getVehiclesRequest = new GetObjectListRequest()
            {
                ClientId = _authorizeHciResponse.ClientId,
                ObjectType = InvariantNames.GetInvariantName<T>()
            };
            var futureResult = _executor.Submit<GetObjectListResponse>(getVehiclesRequest);

            futureResult.Wait();

            if (futureResult.Exception != null)
            {
                _logger.LogError($"GetObjectList => Error on request: {futureResult.Exception.Message}");
                throw futureResult.Exception;
            }

            var response = (GetObjectListResponse)futureResult.Value;
            return response.Objects;
        }


        public void Subscribe<T>(NotificationHandler handler) where T : IIdentifiable
        {
            var prefSubscription = new ObjectModificationSubscription
            {
                ObjectType = InvariantNames.GetInvariantName<T>()
            };

            var prefSubscriptionWrapper = new EventSubscriptionWrapper
            {
                ObjectModificationSubscription = prefSubscription
            };

            var subscribeRequest = new SubscribeEventRequest()
            {
                ClientId = _authorizeHciResponse.ClientId,
                Subscription = prefSubscriptionWrapper
            };

            var futureResult = _executor.Submit<SubscribeEventResponse>(subscribeRequest);

            futureResult.Wait();

            var response = (SubscribeEventResponse)futureResult.Value;
            _notificationListener.AddSubscription(new SubscriptionToken(response.SubscriptionId, handler, prefSubscriptionWrapper));
        }

        public List<MissionPreference> GetMissionPreferences(Mission missionWithIdOnly)
        {
            var missionRequest = new GetMissionPreferencesRequest
            {
                ClientId = _authorizeHciResponse.ClientId, 
                User = LastLoginResponse.User, 
                Mission = missionWithIdOnly
            };

            var futureResult = _executor.Submit<GetMissionPreferencesResponse>(missionRequest);
            futureResult.Wait();

            return futureResult.Value?.Preferences;
        }

        public void SubscribeToTelemetry(NotificationHandler handler)
        {
            var telemetrySubscriptionWrapper = new EventSubscriptionWrapper
            {
                TelemetrySubscription = new TelemetrySubscription()
            };

            var requestTelemetryEvent = new SubscribeEventRequest()
            {
                ClientId = _authorizeHciResponse.ClientId,
                Subscription = telemetrySubscriptionWrapper
            };

            var futureResult = _executor.Submit<SubscribeEventResponse>(requestTelemetryEvent);
            futureResult.Wait();
            var response = (SubscribeEventResponse)futureResult.Value;
            _notificationListener.AddSubscription(new SubscriptionToken(response.SubscriptionId, handler, telemetrySubscriptionWrapper));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
