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
            //{ PelcoDEMessageType.SetPan, 0x71 },
            //{ PelcoDEMessageType.SetTilt, 0x73 },

            { PelcoDEMessageType.RequestPan, 0x51 },
            { PelcoDEMessageType.RequestTilt, 0x53 },
            { PelcoDEMessageType.RequestPanResponse, 0x61 },
            { PelcoDEMessageType.RequestTiltResponse, 0x63 },


            { PelcoDEMessageType.SetPanCompleteResponse, 0x7C },
            { PelcoDEMessageType.SetTiltCompleteResponse, 0x7C },
        };
    }
}