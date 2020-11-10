using System;

namespace sportal.Data
{
	public class User
	{
		public string Subject { get; private set; }

		public string DisplayName { get; private set; }

		public string Email { get; private set; }

		public string Biography { get; private set; }

		public string[] Groups { get; private set; }

		public User(string subject, string displayName, string email, string biography, string[] groups)
		{
			this.Subject = subject;
			this.DisplayName = displayName;
			this.Email = email;
			this.Biography = biography;
			this.Groups = groups;
		}
	}
}
