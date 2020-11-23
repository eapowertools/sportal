using System;

namespace sportal.Data
{
	public class User
	{
		public string Subject { get; private set; }

		public string DisplayName { get; private set; }

		public string Email { get; private set; }

		public string Title { get; private set; }

		public string Biography { get; private set; }

		public string Image { get; private set; }

		public string[] Groups { get; private set; }

		public User(string subject, string displayName, string email, string title, string biography, string image, string[] groups)
		{
			this.Subject = subject;
			this.DisplayName = displayName;
			this.Email = email;
			this.Title = title;
			this.Biography = biography;
			this.Image = image;
			this.Groups = groups;
		}
	}
}
