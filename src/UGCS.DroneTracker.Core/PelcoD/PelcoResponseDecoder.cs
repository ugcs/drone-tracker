using System;
using System.Collections.Generic;
using System.Linq;

namespace UGCS.DroneTracker.Core.PelcoD
{
    public class PelcoResponseDecoder
    {
        private readonly Dictionary<PelcoDEMessageType, byte> _commandsMapping;

        public PelcoResponseDecoder(Dictionary<PelcoDEMessageType, byte> commandsMapping)
        {
            _commandsMapping = commandsMapping ?? DefaultPelcoCodesMappingFactory.CodesMapping;
        }

        public PelcoDEMessageType? GetResponseType(PelcoDEMessage pelcoMessage)
        {
            if (pelcoMessage == null) throw new ArgumentNullException(nameof(pelcoMessage));

            if (_commandsMapping.Values.Contains(pelcoMessage.Command2))
            {
                return _commandsMapping.FirstOrDefault(cm => cm.Value == pelcoMessage.Command2).Key;
            }
            return null;
        }

        public static ushort GetUInt16(PelcoDEMessage pelcoMessage)
        {
            return BitConverter.ToUInt16(new[] { pelcoMessage.DataL, pelcoMessage.DataH });
        }
    }
}