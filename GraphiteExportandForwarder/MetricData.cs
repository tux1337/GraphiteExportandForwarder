using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphiteExportandForwarder
{
    public class MetricData
    {
        public string target;
        public List<dynamic> datapoints;


        public class Datapoint
        {
            public List<dynamic> data;
        }
    }
}
