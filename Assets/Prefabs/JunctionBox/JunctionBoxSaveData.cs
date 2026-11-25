#nullable enable

using System.Collections.Generic;
using System.Xml.Serialization;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Hardwired.Networks;
using Hardwired.Objects.Electrical;
using LibConstruct;

namespace Hardwired.Prefabs.JunctionBox
{
    [XmlInclude(typeof(JunctionBoxSaveData))]
    public class JunctionBoxSaveData : CableSaveSaveData
    {
        [XmlElement]
        public PlacementBoardHostSaveData? Board;

        [XmlElement]
        public List<JunctionBoxConnectionSaveData> JunctionConnections = new();
    }

    [XmlInclude(typeof(JunctionBoxConnectionSaveData))]
    public class JunctionBoxConnectionSaveData
    {
        [XmlElement]
        public int FromConnection;

        [XmlElement]
        public CableSpan.ConductorType FromConductorType;

        [XmlElement]
        public int ToConnection;

        [XmlElement]
        public CableSpan.ConductorType ToConductorType;
    }

    [XmlInclude(typeof(JunctionBoxTerminalBlockSaveData))]
    public class JunctionBoxTerminalBlockSaveData : StructureSaveData
    {
        [XmlElement]
        public PlacementBoardStructureSaveData? Board;
    }
}