using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using NLog;
using System.Threading.Tasks;
using System.Collections.Generic;
using GoogleDomainAPIDNSUpdater.Models;

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

                string ip = GetIP(),
                       json = string.Empty;
                IReadOnlyList<Domain> domains = Enumerable.Empty<Domain>().ToList(),
                                      outdatedDomains = domains;

                InitializeDomainFile();
                json = File.ReadAllText(App.Default.DomainFile);
                domains = JsonConvert.DeserializeObject<IEnumerable<Domain>>(json).ToList();
                outdatedDomains = domains.Where(domain => !string.IsNullOrWhiteSpace(domain.Name))
                                         .Where(domain => !string.Equals(domain.IP, ip))
                                         .ToList();

                if (outdatedDomains.Any())
                {
                    logger.Info($"IP has changed for {string.Join(", ", outdatedDomains)}");
                    var items = outdatedDomains.Select(domain => new { Domain = domain, Task = UpdateGoogleDNS(ip, domain.Name) });

                    Task.WhenAll(items.Select(domain => domain.Task)).Wait();
                    foreach(var item in items) { if (item.Task.Result) { item.Domain.IP = ip; } }

                    if (items.Select(item => item.Task.Result).Any(result => result))
                    {
                        logger.Info("Saving new IP to file...");
                        File.WriteAllText(App.Default.DomainFile, JsonConvert.SerializeObject(domains));
                        logger.Info("Done saving new IP to file...");
                    }
                    else
                    {
                        logger.Warn("DNS update was not successful. Check the log for more info.");
                    }
                }
                else if (!domains.Any())
                {
                    logger.Info("No domains found.");
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
            catch (AggregateException exs)
            {
                foreach(var ex in exs.InnerExceptions) { logger.Error(ex); }
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
        static async Task<bool> UpdateGoogleDNS(string ip, string domain)
        {
            var tcs = new TaskCompletionSource<bool>();

            logger.Info("Updating Google DNS...");
            using (var client = new WebClient())
            {
                byte[] crendentials = Encoding.ASCII.GetBytes($"{App.Default.Username}:{App.Default.Password}");
                Uri relativeUri = new Uri(App.Default.RelativePath, UriKind.Relative);
                
                client.QueryString = new NameValueCollection();
                client.QueryString.Add("hostname", domain);
                client.QueryString.Add("myip", ip);
                logger.Info($"hostname: {domain}, myip: {ip}");
                client.BaseAddress = App.Default.BaseAddress;
                client.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(crendentials)}");
                client.UploadStringCompleted += (sender, args) =>
                {
                    if (args.Error == null)
                    {
                        logger.Info($"API Results: {args.Result}");
                        tcs.SetResult(string.Equals(args.Result.ToLower(), $"good {ip}") || string.Equals(args.Result.ToLower(), $"nochg {ip}"));
                    }
                    else
                    {
                        logger.Error(args.Error, $"Error occurred when trying to update {domain}");
                        tcs.SetException(args.Error);
                    }
                };
                client.UploadStringAsync(relativeUri, "POST");
            }
            logger.Info("Done updating Google DNS...");

            return await tcs.Task;
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

        static void InitializeDomainFile()
        {
            IEnumerable<Domain> defaultDomain = new List<Domain> { new Domain("0.0.0.0", "_domain_") };
            if (string.IsNullOrWhiteSpace(App.Default.DomainFile)) { throw new ArgumentException($"'{nameof(App.Default.DomainFile)}' setting is blank."); }
            if (!File.Exists(App.Default.DomainFile)) { File.WriteAllText(App.Default.DomainFile, JsonConvert.SerializeObject(defaultDomain)); };
        }
    }
}
