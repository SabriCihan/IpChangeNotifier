using System;
using System.IO;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Configuration;
using static IpChangeNotifier.Program;

namespace IpChangeNotifier
{
    class Program
    {
        private static string lastIpAddress = string.Empty;
        private static readonly HttpClient httpClient = new HttpClient();
        private static System.Threading.Timer timer;
        private static Mailsettings mailSettings;
        private static int checkInterval;
        private static bool isPaused = false;
        private static bool isFirstCheck = true;
        static async Task Main(string[] args)
        {
            var checkInterval = LoadConfiguration();

            Console.WriteLine($"{DateTime.Now.ToString()} | IP Change Notifier started.Interval: {checkInterval} minutes.Press 'P' key for pause");
            timer = new System.Threading.Timer(CheckIpAddress, null, 0, Timeout.Infinite);
            while (true)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.P)
                {
                    if (isPaused)
                    {
                        Console.WriteLine($"{DateTime.Now.ToString()} | Resuming IP check...");
                        isPaused = false;
                        timer.Change(0, Timeout.Infinite);
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now.ToString()} | Pausing IP check...");
                        isPaused = true;
                        timer.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                }
            }
        }
        private static int LoadConfiguration()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", optional: false, reloadOnChange: true)
                .Build();

            mailSettings = config.GetSection("Mailsettings").Get<Mailsettings>();
            var min = config.GetValue<int>("IpCheckIntervalMinutes");
            checkInterval = min * 60000; // Convert minutes to milliseconds
            return min;
        }

        private static async void CheckIpAddress(object state)
        {
            if (isPaused) return;

            try
            {
                string currentIpAddress = await GetPublicIpAddressAsync();
                if (isFirstCheck)
                {
                    lastIpAddress = currentIpAddress;
                    isFirstCheck = false;
                }
                if (!string.IsNullOrEmpty(currentIpAddress) && currentIpAddress != lastIpAddress)
                {
                    Console.WriteLine($"{DateTime.Now.ToString()} | Oooooooo IP Address is changed : {lastIpAddress} -> {currentIpAddress}");
                    SendEmail(mailSettings.SenderEmail, mailSettings.Password, mailSettings.To1, $"IP Address Change", $"{DateTime.Now.ToString()} | Your IP address has changed {lastIpAddress} -> {currentIpAddress}");
                    lastIpAddress = currentIpAddress;
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now.ToString()} | IP Address is same: {currentIpAddress}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now.ToString()} | Error checking IP address: {ex.Message}");
            }
            finally
            {
                if (!isPaused)
                {
                    timer.Change(checkInterval, Timeout.Infinite);
                }
            }
        }

        private static async Task<string> GetPublicIpAddressAsync()
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync("https://api.ipify.org");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now.ToString()} | Error fetching public IP address: {ex.Message}");
                return null;
            }
        }

        private static void SendEmail(string from, string password, string to, string subject, string body)
        {
            try
            {
                MailMessage mail = new MailMessage(from, to, subject, body);
                SmtpClient client = new SmtpClient(mailSettings.Server, mailSettings.Port)
                {
                    Credentials = new System.Net.NetworkCredential(from, password),
                    EnableSsl = mailSettings.EnableSsl
                };
                client.Send(mail);
                Console.WriteLine($"{DateTime.Now.ToString()} | Email sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now.ToString()} | Error sending email: {ex.Message}");
            }
        }

         

        public class Mailsettings
        {
            public string Server { get; set; }
            public int Port { get; set; }
            public string SenderName { get; set; }
            public string SenderEmail { get; set; }
            public string Password { get; set; }
            public string To1 { get; set; }
            public string To2 { get; set; }
            public string To3 { get; set; }
            public bool EnableSsl { get; set; }

        }


    }
}
