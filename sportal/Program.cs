using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace sportal
{
	internal class GlobalSettings
	{
		internal static bool HIDE_SETTINGS = false;

		internal static int DEFAULT_PORT = 8080;
	}

	public class Program
	{
		public static void Main(string[] args)
		{
			int portNumber = GlobalSettings.DEFAULT_PORT;
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "--port" || args[i] == "-p")
				{
					i = i + 1;
					if (Int32.TryParse(args[i], out portNumber))
					{
						Console.WriteLine("Custom port used: " + args[i]);
					}
					else
					{
						Console.WriteLine("Failed to parse port number: " + args[i] + ". Will quit.");
						return;
					}
				}
				else if (args[i] == "--hidesettings")
				{
					GlobalSettings.HIDE_SETTINGS = true;
				}
			}


			if (args.Length == 2)
			{
				if (args[0] == "--port" || args[0] == "-p")
				{
					if (Int32.TryParse(args[1], out portNumber))
					{
						Console.WriteLine("Custom port used: " + args[1]);
					}
					else
					{
						Console.WriteLine("Failed to parse port number: " + args[1] + ". Will quit.");
						return;
					}
				}
			}
			CreateHostBuilder(args, portNumber).Build().Run();

		}

		public static IHostBuilder CreateHostBuilder(string[] args, int portNumber) => Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.UseStartup<Startup>().UseUrls("http://*:" + portNumber.ToString());
				});
	}
}
