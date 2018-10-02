using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Graphite;
using System.Threading;

namespace GraphiteExportandForwarder
{
    class Program
    {
        private static CredentialCache GetCredential(string url)
        {
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;
            CredentialCache credentialCache = new CredentialCache();
            credentialCache.Add(new System.Uri(url), "Basic", new NetworkCredential(Properties.Settings.Default.GraphiteSourceUsername, Properties.Settings.Default.GraphiteSourcePassword));
            return credentialCache;
        }

        private static String getContent(string url)
        {
            string html = string.Empty;

            if (Properties.Settings.Default.DoNotValidateSSLCert == "true")
            {
                ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            if (!String.IsNullOrEmpty(Properties.Settings.Default.GraphiteSourceUsername))
            {
                request.Credentials = GetCredential(url);
                request.PreAuthenticate = true;
            }
            //30 min timeout
            request.Timeout = 1800000;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }

            return html;
        }

        //works not on all Graphite implementations
        private static List<String> getAllMetrics(string mainurl)
        {
            string metricurl = mainurl + "metrics/index.json";

            List<String> back = JsonConvert.DeserializeObject<List<String>>(getContent(metricurl));

            return back;
        }

        private static bool getAllMetrics(string mainurl, string querymetric, List<String> metrics)
        {
            string metricurl = mainurl + "metrics/find?query=";
            List<Metric> back = JsonConvert.DeserializeObject<List<Metric>>(getContent(metricurl + querymetric));

            List<Task> tasks = new List<Task>();

            foreach(Metric m in back)
            {
                if(m.expandable == 0)
                    metrics.Add(m.id);

                if (m.expandable == 1)
                {
                    tasks.Add(Task<bool>.Factory.StartNew(() => { return getAllMetrics(mainurl, m.id + ".*", metrics); }));
                    //getAllMetrics(mainurl, m.id + ".*", metrics);
                }
            }

            Task.WaitAll(tasks.ToArray());

            return true;
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        private static bool pushMetrictonewGraphite(string metric, string oldmainurl, string newgraphite, int newport)
        {
            string url = oldmainurl + "render?target=" + metric + "&format=json&from="+ Properties.Settings.Default.ImportDataFromDateon +"&until=now";
            List<MetricData> back = JsonConvert.DeserializeObject<List<MetricData>>(getContent(url));

            string prefix = metric.Remove(metric.LastIndexOf("."));
            string metricname = metric.Remove(0, metric.LastIndexOf(".")+1);

            var graphiteclient = new Graphite.GraphiteTcpClient(newgraphite, newport, prefix);

            foreach (MetricData m in back)
            {
                foreach (dynamic e in m.datapoints)
                {
                    string value = Convert.ToString(e[0]);
                    double time = Convert.ToDouble(e[1]);

                    if (!String.IsNullOrEmpty(value))
                    {
                        try
                        {
                            graphiteclient.Send(metricname, Convert.ToDouble(value), UnixTimeStampToDateTime(time));
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine("Error sending metric to Graphite: " + metricname + "; Exception: " + err.Message);
                        }
                    }
                        

                }
            }

            if(String.IsNullOrEmpty(metric))
                Console.WriteLine("Error: Metric is empty!");

            return true;
        }

        private static long seclastprint = 0;
        private static void printStatus(int metricsleft, int metricsdone, int runningtasks, int maxtasks)
        {
            if ((DateTime.Now.Ticks / 10000000) - seclastprint >= Convert.ToInt32(Properties.Settings.Default.printStatuseveryXSeconds.ToString()) || seclastprint == 0)
            {
                Console.WriteLine(metricsleft.ToString() + " metrics left / " + metricsdone.ToString() + " metrics done / " + runningtasks.ToString() + " Threads running / max Threads " + maxtasks.ToString());
                seclastprint = (DateTime.Now.Ticks / 10000000);
            }
        }

        
        static void Main(string[] args)
        {
            int maxThreads = Convert.ToInt32(Properties.Settings.Default.maxThreads.ToString());
            string mainurl = Properties.Settings.Default.GraphiteSourceServer;

            string newgraphite = Properties.Settings.Default.GraphiteDestinationServer;
            int newport = Convert.ToInt32(Properties.Settings.Default.GraphiteDestinationCarbonPort);
            
            int startmetric = Convert.ToInt32(Properties.Settings.Default.startmetric.ToString());

            List<String> metrics = new List<string>();

            try
            {
                metrics = getAllMetrics(mainurl);
            }
            catch
            {
                Console.WriteLine("Not possible to get all metrics from graphite over index.json. Use query API instead... ");

                getAllMetrics(mainurl, "*", metrics);
            }
                
                
            List<Task> runningTasks = new List<Task>();
            int i = 0;
            Task<int> readmaxThreads = null;

            //delete metrics
            if (startmetric > 0)
            {
                metrics.RemoveRange(0, startmetric);
                i = startmetric;
                i++;
            }

            while(metrics.Count > 0)
            {
                if(metrics[0].StartsWith(Properties.Settings.Default.OnlyImportMetricsThatStartsWith) || String.IsNullOrEmpty(Properties.Settings.Default.OnlyImportMetricsThatStartsWith))
                    runningTasks.Add(Task<bool>.Factory.StartNew(() => { return pushMetrictonewGraphite(metrics[0], mainurl, newgraphite, newport); }));

                metrics.RemoveAt(0);
                i++;

                printStatus(metrics.Count, i, runningTasks.Count, maxThreads);

                if (readmaxThreads == null)
                    readmaxThreads = Task<int>.Factory.StartNew(() =>
                    {
                        int back = maxThreads;
                        while (true)
                        {
                            string input = Console.ReadLine();
                            try
                            {
                                back = Convert.ToInt32(input);
                                break;
                            }
                            catch { }
                        }
                        return back;
                    });
                else if (readmaxThreads.IsCompleted)
                {
                    maxThreads = readmaxThreads.Result;
                    readmaxThreads = null;
                }

                bool wait = false;
                while (runningTasks.Count >= maxThreads)
                {
                    printStatus(metrics.Count, i, runningTasks.Count, maxThreads);

                    //warte eine 10ms
                    if (wait)
                        Thread.Sleep(10);

                    //entferne alle fertigen tasks
                    runningTasks.RemoveAll(x => x.IsCompleted);

                    wait = true;
                }
            }


            while (runningTasks.Count > 0)
            {
                runningTasks.RemoveAll(x => x.IsCompleted);

                //warte 1 minute um dann nochmals zu prüfen... 
                Thread.Sleep(60000);
            }

            Console.WriteLine("Export complete. Press a key to exit.");
            Console.ReadLine();
        }
    }
}
