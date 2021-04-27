using System;

namespace sportal.EmbeddedBlazorContentHelpers
{
	public class SportalException : Exception
	{
		public SportalException(string message) : base(message)
		{
		}

		public override string StackTrace
		{
			get
			{
				return "";
			}
		}
	}
}
