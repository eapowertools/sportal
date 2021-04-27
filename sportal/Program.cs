using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

using sportal.EmbeddedBlazorContentHelpers;

namespace sportal
{
	internal class GlobalSettings
	{
		internal static readonly int DEFAULT_HTTPS_PORT = 8443;

		internal static bool HIDE_SETTINGS = false;

		internal static int PORT = 8080;

		internal static bool IS_HTTPS = false;

		internal static string CERTIFICATE_PATH = "";

		internal static SecureString CERTIFICATE_PASSWORD = null;
	}

	public class Program
	{
		public static void Main(string[] args)
		{
			int portNumber = 0;
			bool newPort = false;
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "--port" || args[i] == "-p")
				{
					newPort = true;
					i++;
					if (Int32.TryParse(args[i], out portNumber))
					{
						GlobalSettings.PORT = portNumber;
						Console.WriteLine("Custom port used: " + args[i]);
					}
					else
					{
						Console.WriteLine("Failed to parse port number: " + args[i] + ".");
						throw new Exception("Exiting...");
					}
				}
				else if (args[i] == "--certificate" || args[i] == "-c")
				{
					GlobalSettings.IS_HTTPS = true;
					i++;
					GlobalSettings.CERTIFICATE_PATH = args[i];
					if (!File.Exists(GlobalSettings.CERTIFICATE_PATH))
					{
						Console.WriteLine("Certificate file does not exist, or Sportal does not have access to it.\nLocation: '" + GlobalSettings.CERTIFICATE_PATH + "'");
						throw new Exception("Exiting...");
					}

					i++;
					GlobalSettings.CERTIFICATE_PASSWORD = new SecureString();
					foreach (char c in args[i])
					{
						GlobalSettings.CERTIFICATE_PASSWORD.AppendChar(c);
					}
					GlobalSettings.CERTIFICATE_PASSWORD.MakeReadOnly();
				}
				else if (args[i] == "--hidesettings")
				{
					GlobalSettings.HIDE_SETTINGS = true;
				}
				else
				{
					Console.WriteLine("Invalid argument: '" + args[i] + "'");
					throw new Exception("Exiting...");

				}
			}
			if (!newPort)
			{
				if (GlobalSettings.IS_HTTPS)
				{
					GlobalSettings.PORT = GlobalSettings.DEFAULT_HTTPS_PORT;
				}
			}

			CreateHostBuilder(args).Build().Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.UseStartup<Startup>().UseKestrel(options => options.ConfigureServer());
				}
			);
	}

	public static class KestrelServerOptionsExtensions
	{
		public static void ConfigureServer(this KestrelServerOptions options)
		{
			string host = "localhost";

			var ipAddresses = new List<IPAddress>();
			if (host == "localhost")
			{
				ipAddresses.Add(IPAddress.IPv6Loopback);
				ipAddresses.Add(IPAddress.Loopback);
			}
			else if (IPAddress.TryParse(host, out var address))
			{
				ipAddresses.Add(address);
			}
			else
			{
				ipAddresses.Add(IPAddress.IPv6Any);
			}

			foreach (var address in ipAddresses)
			{
				options.Listen(address, GlobalSettings.PORT,
					listenOptions =>
					{
						if (GlobalSettings.IS_HTTPS)
						{
							X509Certificate2 certificate = LoadCertificate();
							listenOptions.UseHttps(certificate);
						}
					});
			}
		}

		private static X509Certificate2 LoadCertificate()
		{
			// If I want to add certificate store functionality later
			//if (config.StoreName != null && config.StoreLocation != null)
			//{
			//	using (var store = new X509Store(config.StoreName, Enum.Parse<StoreLocation>(config.StoreLocation)))
			//	{
			//		store.Open(OpenFlags.ReadOnly);
			//		var certificate = store.Certificates.Find(
			//			X509FindType.FindBySubjectName,
			//			config.Host,
			//			validOnly: false);

			//		if (certificate.Count == 0)
			//		{
			//			throw new InvalidOperationException($"Certificate not found for {config.Host}.");
			//		}

			//		return certificate[0];
			//	}
			//}
			//else if (config.FilePath != null && config.Password != null)
			//{
			//	new X509Certificate2()
			//	return new X509Certificate2(config.FilePath, config.Password);
			//}
			//throw new InvalidOperationException("No valid certificate configuration found for the current endpoint.");

			X509Certificate2 cert = null;
			try
			{
				cert = new X509Certificate2(GlobalSettings.CERTIFICATE_PATH, GlobalSettings.CERTIFICATE_PASSWORD);
			}
			catch (Exception e)
			{
				Console.WriteLine("Failed to load certificate from: '" + GlobalSettings.CERTIFICATE_PATH + "'");
				Console.WriteLine("Exception: " + e.Message);

				throw new SportalException("Exiting...");
			}
			return cert;
		}
	}
}
