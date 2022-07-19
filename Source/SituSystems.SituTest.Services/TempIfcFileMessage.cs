using System;
using System.Runtime.Serialization;

namespace SituSystems.SituTest.Services
{
    [DataContract]
    public class TempIfcFileMessage
    {
        [DataMember]
        public string BlobUrl { get; set; }

        [DataMember]
        public string Builder { get; set; }

        [DataMember]
        public string HomeName { get; set; }

        [DataMember]
        public string FileName { get; set; }

        [DataMember]
        public IfcType IfcType { get; set; }
    }
}
