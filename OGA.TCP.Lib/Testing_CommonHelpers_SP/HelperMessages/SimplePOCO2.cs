using System;
using System.Collections.Generic;
using System.Text;

namespace Testing_CommonHelpers_SP.Helpers
{
    public class SimplePOCO2
    {
        public string KeyId { get; set; }

        public string Algo { get; set; }

        public string SignatureB64 { get; set; }

        public SimplePOCO2()
        {
            KeyId = "";
            Algo = "";
            SignatureB64 = "";
        }
    }
}
