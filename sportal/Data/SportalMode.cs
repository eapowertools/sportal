using System;

namespace sportal.Data
{
	public enum SportalMode
	{
		// Read users.json and impersonate arbitrary subjects (original behavior).
		Local,
		// Read active users and groups from the tenant on each refresh; users.json is neither read nor written.
		Impersonate,
		// Fetch from the tenant and write the result into users.json (hand-written title, biography and image are kept).
		Sync
	}

	public static class SportalModeExtensions
	{
		public static bool UsesTenant(this SportalMode mode)
		{
			return mode == SportalMode.Impersonate || mode == SportalMode.Sync;
		}
	}
}
