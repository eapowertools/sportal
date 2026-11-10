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

		public string ImageURL { get; private set; }

		public string[] Groups { get; private set; }

		// Tenant user id and role names; only known for users read from the tenant.
		public string Id { get; private set; }

		public string[] Roles { get; private set; }

		public User(string subject, string displayName, string email, string title, string biography, string image, string imageURL, string[] groups, string id = null, string[] roles = null)
		{
			this.Id = id;
			this.Roles = roles ?? new string[0];
			this.Subject = subject;
			this.DisplayName = displayName;
			this.Email = email;
			this.Title = title;
			this.Biography = biography;
			this.Image = image;
			this.ImageURL = imageURL;
			this.Groups = groups;
		}

		public bool HasImageURL()
		{ 
			if (string.IsNullOrEmpty(ImageURL))
			{
				return false;
			}
			return true;
		}
	}
}
