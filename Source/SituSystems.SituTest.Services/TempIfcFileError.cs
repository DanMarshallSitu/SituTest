using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SituSystems.SituTest.Services
{
    [DataContract]
    public class TempIfcFileError
    {
        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public DateTime ErrorDate { get; set; }

        [DataMember]
        public string FileName { get; set; }

        [DataMember]
        public string Home { get; set; }

        [DataMember]
        public List<string> ErrorMessages { get; set; }
    }
}
