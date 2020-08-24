using System.Collections.Generic;

namespace UGCS.DroneTracker.Core.PelcoD
{
    public static class DefaultPelcoCodesMappingFactory
    {
        public static readonly Dictionary<PelcoDEMessageType, byte> CodesMapping = new Dictionary<PelcoDEMessageType, byte>()
        {
            { PelcoDEMessageType.Stop, 0x00 },
            { PelcoDEMessageType.TiltUp, 0x08 },
            { PelcoDEMessageType.TiltDown, 0x10 },
            { PelcoDEMessageType.PanLeft, 0x04 },
            { PelcoDEMessageType.PanRight, 0x02 },
            { PelcoDEMessageType.SetPan, 0x4B },
            { PelcoDEMessageType.SetTilt, 0x4D },

            { PelcoDEMessageType.RequestPan, 0x51 },
            { PelcoDEMessageType.RequestTilt, 0x53 },

            { PelcoDEMessageType.RequestPanResponse, 0x59 },
            { PelcoDEMessageType.RequestTiltResponse, 0x5B },


            { PelcoDEMessageType.SetPanCompleteResponse, 0x7C },
            { PelcoDEMessageType.SetTiltCompleteResponse, 0x7C },
        };
    }
}