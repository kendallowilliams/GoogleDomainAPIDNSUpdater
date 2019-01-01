using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace GoogleDomainAPIDNSUpdater
{
    class Program
    {
        static ILogger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            logger.Info("Starting Google DNS Update Utility...");
            try
            {
                string ip = GetIP();

                if (IPChanged(ip))
                {
                    logger.Info("IP has changed.");
                    if (UpdateGoogleDNS(ip))
                    {
                        logger.Info("Saving new IP to file...");
                        File.WriteAllText(App.Default.IPFile, ip);
                        logger.Info("Done saving new IP to file...");
                    }
                    else
                    {
                        logger.Warn("DNS update was not successful. Check the log for more info.");
                    }
                }
                else
                {
                    logger.Info("IP has not changed. No update required.");
                }
            }
            catch (WebException ex)
            {
                logger.Error(ex);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            logger.Info("Closing Google DNS Update Utility...");
        }

        /*
            https://support.google.com/domains/answer/6147083?hl=en 
        */
        static bool UpdateGoogleDNS(string ip)
        {
            bool updated = false;

            logger.Info("Updating Google DNS...");
            using (var client = new WebClient())
            {
                byte[] crendentials = Encoding.ASCII.GetBytes($"{App.Default.Username}:{App.Default.Password}");
                string results = string.Empty;
                Uri relativeUri = new Uri(App.Default.RelativePath, UriKind.Relative);
                
                client.QueryString = new NameValueCollection();
                client.QueryString.Add("hostname", App.Default.Domain);
                client.QueryString.Add("myip", ip);
                logger.Info($"hostname: {App.Default.Domain}, myip: {ip}");
                client.BaseAddress = App.Default.BaseAddress;
                client.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(crendentials)}");
                results = client.UploadString(relativeUri, "POST");
                logger.Info($"API Results: {results}");
                updated = string.Equals(results.ToLower(), $"good {ip}") || string.Equals(results.ToLower(), $"nochg {ip}");
            }
            logger.Info("Done updating Google DNS...");

            return updated;
        }

        static bool IPChanged(string ip)
        {
            bool hasChanged = false;
            string currentIP = string.Empty;

            logger.Info("Checking if IP has changed...");
            if (File.Exists(App.Default.IPFile))
            {
                logger.Info("Reading IP in file...");
                currentIP = File.ReadAllText(App.Default.IPFile);
                logger.Info($"Current: {currentIP}, New: {ip}");
                logger.Info("Done reading IP in file...");
                hasChanged = !string.Equals(currentIP, ip);
            }
            else
            {
                logger.Info($"Existing IP address not found in file.");
                hasChanged = true;
            }
            logger.Info("Done checking IP for changes...");

            return hasChanged;
        }

        static string GetIP()
        {
            string ip = string.Empty;

            logger.Info("Getting public IP address...");
            using (var client = new WebClient())
            {
                Uri uri = new Uri("http://api.ipify.org/");

                ip = client.DownloadString(uri);
                logger.Info($"IP Address: {ip}");
            }
            logger.Info("Finished getting public IP address...");

            return ip;
        }
    }
}
