using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PocTrace.Core.Models
{
    public class RAWDATA_REQ_EXCEL
    {
        public int totalConnectorCount { get; set; }
        public int totalInvalidCount{ get; set; }
        public int invalidCountForHU{ get; set; }
        public string InspectResult{ get; set; }
        public string feedback{ get; set; }

        public string status{ get; set; }
        public string connectorUse{ get; set; }
        public string connectorName{ get; set; }
        public string sTag{ get; set; }
        public string tTag{ get; set; }
        public string tConnectionTag{ get; set; }
        public string cGUID{ get; set; }
        public string sUT{ get; set; }
        public string sConnectedUT{ get; set; }
        public string tConnectedUT{ get; set; }
        public string tUT{ get; set; }
        public string tUTinLN{ get; set; } //UT
        public string sUTGroup{ get; set; }
        public string sConnectorTag{ get; set; }
        public string HUResult{ get; set; }
        public string tName{ get; set; }
        public string sModuleName{ get; set; }
        public string sPOCNo{ get; set; }
        public string sSubDivision1{ get; set; }
        public string sSubDivision2{ get; set; }
        public string tDivision{ get; set; }
        public string tSubDivision1{ get; set; }
        public string tSubDivision2{ get; set; }
        public string sName{ get; set; }

        public string sGUID{ get; set; }
        public string tGUID{ get; set; }

        public string ModelerComments{ get; set; }

        public string sConSize{ get; set; }
        public string tModuleName{ get; set; }
        public string tPOCNo{ get; set; }

        public string Modeler{ get; set; }

        public string UpdateDate{ get; set; }
        public string Project{ get; set; }

        public string EQPID{ get; set; }
    }
}
