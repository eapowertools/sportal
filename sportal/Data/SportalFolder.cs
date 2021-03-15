using System;
using System.Diagnostics;
using System.IO;

namespace sportal.Data
{
	public static class SportalFolder
	{
		private static string _workingDirectory;

		public static string WorkingDirectory
		{
			get
			{
				if (string.IsNullOrEmpty(_workingDirectory))
				{
					_workingDirectory = Directory.GetCurrentDirectory();
					if (!Debugger.IsAttached && Environment.OSVersion.Platform == PlatformID.Unix)
					{
						string filePath = Process.GetCurrentProcess().MainModule.FileName;
						int indexOfSlash = filePath.LastIndexOf('/');
						_workingDirectory = filePath.Substring(0, indexOfSlash);
					}
				}
				return _workingDirectory;
			}
		}
	}
}
