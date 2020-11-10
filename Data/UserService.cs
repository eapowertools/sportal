using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace sportal.Data
{
	public class UserService
	{
		private List<User> _users;

		public UserService()
		{
			_users = new List<User>();
			_users.Add(new User("someSubject", "Jesse Paris", "jparis@dsk/com", new string[] { "Administrator" }));
			_users.Add(new User("someSubject", "Ctripp Paris", "jparis@dsk/com", new string[] { "Administrator" }));
			_users.Add(new User("someSubject", "some other user", "jparis@dsk/com", new string[] { "Administrator" }));
			_users.Add(new User("someSubject", "fdsfdsfs", "jparis@dsk/com", new string[] { "Administrator" }));

		}

		public Task<User[]> GetUserListAsync()
		{
			return Task.FromResult(_users.ToArray());
		}

	}
}
