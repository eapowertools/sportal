using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace sportal
{
	public class Program
	{
		private static int DEFAULT_PORT = 8080;

		public static void Main(string[] args)
		{
			int portNumber = DEFAULT_PORT;
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
