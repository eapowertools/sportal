using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using sportal.Data;

namespace sportal.Services
{
	public class UserService
	{
		private static string DEFAULT_USERS =
@"[
{
    'subject': 'SaaSP-e797d094-716a-436d-a56b-e67144c5fbb4',
    'name': 'Emily Tiernen',
    'email': 'emily.tiernen@saaspersonas.dev',
    'title': 'CEO',
    'biography': 'I need access to applications for my Executive team and all Sales data.',
    'groups': ['Executive', 'North', 'South']
},
{
    'subject': 'SaaSP-91a34244-b089-43b2-a08e-069df72eb1c5',
    'name': 'Landon Bozak',
    'email': 'landon.bozak@saaspersonas.dev',
    'title': 'Sales Director',
    'biography': 'I manage, plan, and forecast the North and South regions, and work closely with our Marketing team.',
    'groups': ['Sales', 'North', 'South', 'Marketing']
},
{
    'subject': 'SaaSP-80ae665b-6f3c-4b7b-a020-eef0ceabd27f',
    'name': 'Claire Palmer',
    'email': 'claire.palmer@saaspersonas.dev',
    'title': 'Marketing Lead - South',
    'biography': 'I analyze and execute Marketing campaigns and strategy for the South Region, working closely with Sales to ensure we are successfully meeting our company goals.',
    'groups': ['Sales', 'South', 'Marketing']
},
{
    'subject': 'SaaSP-653058f0-f910-44c7-9799-151f8b8e7436',
    'name': 'Ryan Caruso',
    'email': 'ryan.caruso@saaspersonas.dev',
    'title': 'Supply Chain Manager',
    'biography': 'I execute our raw materials sourcing and purchasing strategies. I also help forecast demand and finished goods inventory for our manufacturing processes.',
    'groups': ['Supply Chain']
},
{
    'subject': 'SaaSP-2cd2018a-75ee-4a38-b97e-b7f36a698f34',
    'name': 'Jonathan Sway',
    'email': 'jonathan.sway@saaspersonas.dev',
    'title': 'Analytics COE Director',
    'biography': 'I manage our Analytics Center of Excellence, and work closely with our business stakeholders to produce actionable insights that drive value to our customers.',
    'groups': ['Qlik Developer', 'IT']
},
{
    'subject': 'SaaSP-3e763aba-08b2-40b0-975d-8845c1d666e3',
    'name': 'Jerry Bly',
    'email': 'jerry.bly@saaspersonas.dev',
    'title': 'External Consultant Dev',
    'biography': 'I\'ve been contracted by the company to assist in a Marketing Analytics review.',
    'groups': ['External']
},
{
    'subject': 'SaaSP-6d61a4e2-bcf1-4bae-934b-0af5206f218d',
    'name': 'Ted Graff',
    'email': 'ted.graff@saaspersonas.dev',
    'title': 'Qlik Developer',
    'biography': 'I develop, test, and productionalize Qlik applications for our business stakeholders.',
    'groups': ['Qlik Developer', 'IT']
}
]";
		private List<User> _users;

		public UserService()
		{
			_users = new List<User>();

			string userString = "";
			if (File.Exists("users.json"))
			{
				userString = File.ReadAllText("users.json");

			}
			else
			{
				userString = DEFAULT_USERS;
			}
			JArray userArray = JArray.Parse(userString);

			foreach (JObject userObject in userArray)
			{
				string sub = userObject["subject"].ToString();
				string name = userObject["name"].ToString();
				string email = userObject["email"].ToString();
				string title = userObject["title"].ToString();
				string bio = userObject["biography"].ToString();
				IEnumerable<JToken> groups = userObject["groups"].Children();

				List<string> groupList = new List<string>();
				foreach (JToken group in groups)
				{
					groupList.Add(group.ToString());
				}

				_users.Add(new User(sub, name, email, title, bio, groupList.ToArray()));

			}

		}

		public Task<User[]> GetUserListAsync()
		{
			return Task.FromResult(_users.ToArray());
		}
	}
}
