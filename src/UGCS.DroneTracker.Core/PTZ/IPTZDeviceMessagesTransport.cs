using System;
using System.Threading.Tasks;
using UGCS.DroneTracker.Core.PelcoD;

namespace UGCS.DroneTracker.Core.PTZ
{
    public interface IPTZDeviceMessagesTransport
    {
        Task Initialize();
        void SendMessage(IPTZMessage message);
        void Teardown();

        event EventHandler<byte[]> MessageReceived;
        event EventHandler<byte[]> MessageSending;

        event EventHandler<bool> ConnectionStatusChanged;
    }
}
