using Newtonsoft.Json.Linq;
using sportal.Services;
using Xunit;

namespace sportal.Tests
{
	public class TenantUserMapperTests
	{
		private static JObject[] Groups() => new[]
		{
			JObject.Parse(@"{ 'id': 'g1', 'name': 'Sales', 'providerType': 'idp' }"),
			JObject.Parse(@"{ 'id': 'g2', 'name': 'Managed', 'providerType': 'custom' }")
		};

		[Fact]
		public void ToUserEntries_MapsFieldsAndResolvesIdpGroupsOnly()
		{
			var users = new[]
			{
				JObject.Parse(@"{ 'subject': 'auth0|1', 'name': 'Jane', 'email': 'jane@x.com',
					'id': 'uid-1',
					'assignedRoles': [ { 'name': 'Analyzer' }, { 'name': 'Developer' }, { 'name': 'Analyzer' } ],
					'assignedGroups': [ { 'id': 'g1' }, { 'id': 'g2' }, { 'id': 'everyone' } ] }")
			};

			JArray entries = TenantUserMapper.ToUserEntries(users, Groups());

			JObject e = (JObject)Assert.Single(entries);
			Assert.Equal("auth0|1", (string)e["subject"]);
			Assert.Equal("Jane", (string)e["name"]);
			Assert.Equal("jane@x.com", (string)e["email"]);
			Assert.Equal("", (string)e["title"]);
			Assert.Equal("uid-1", (string)e["id"]);
			Assert.Equal(new[] { "Analyzer", "Developer" }, e["roles"].ToObject<string[]>());
			Assert.Equal(new[] { "Sales" }, e["groups"].ToObject<string[]>());
		}

		[Fact]
		public void ToUserEntries_AppliesFallbacks()
		{
			var users = new[]
			{
				JObject.Parse(@"{ 'subject': 's1', 'email': 'a@b.com' }"),
				JObject.Parse(@"{ 'subject': 's2' }"),
				JObject.Parse(@"{ 'name': 'No subject' }")
			};

			JArray entries = TenantUserMapper.ToUserEntries(users, Groups());

			Assert.Equal(2, entries.Count);
			Assert.Equal("a@b.com", (string)entries[0]["name"]);
			Assert.Equal("", (string)entries[0]["title"]);
			Assert.Empty(entries[0]["roles"]);
			Assert.Null(entries[0]["id"]);
			Assert.Equal("s2", (string)entries[1]["name"]);
			Assert.Null(entries[1]["email"]);
			Assert.Empty(entries[1]["groups"]);
		}

		[Fact]
		public void Merge_KeepsLocalFieldsAndUpdatesTenantFields()
		{
			var existing = JArray.Parse(@"[
				{ 'subject': 'auth0|1', 'name': 'Old', 'email': 'old@x.com', 'title': 'CEO', 'biography': 'bio', 'image': 'me.png', 'groups': ['A'] },
				{ 'name': 'Demo Person', 'title': 'Demo', 'groups': [] }
			]");
			var tenant = JArray.Parse(@"[
				{ 'subject': 'auth0|1', 'id': 'uid-1', 'name': 'New', 'email': 'new@x.com', 'title': '', 'groups': ['B'], 'roles': ['Developer'] },
				{ 'subject': 'auth0|2', 'name': 'Added', 'title': 'Analyzer', 'groups': [] }
			]");

			JArray merged = TenantUserMapper.Merge(existing, tenant);

			Assert.Equal(3, merged.Count);
			Assert.Equal("New", (string)merged[0]["name"]);
			Assert.Equal("new@x.com", (string)merged[0]["email"]);
			Assert.Equal(new[] { "B" }, merged[0]["groups"].ToObject<string[]>());
			Assert.Equal("uid-1", (string)merged[0]["id"]);
			Assert.Equal(new[] { "Developer" }, merged[0]["roles"].ToObject<string[]>());
			Assert.Equal("CEO", (string)merged[0]["title"]);
			Assert.Equal("bio", (string)merged[0]["biography"]);
			Assert.Equal("me.png", (string)merged[0]["image"]);
			Assert.Equal("Demo Person", (string)merged[1]["name"]);
			Assert.Equal("auth0|2", (string)merged[2]["subject"]);
		}

		[Fact]
		public void GetStockUsersJson_IsStandardJsonWithRequiredFields()
		{
			string json = sportal.Services.UserService.GetStockUsersJson();

			JArray users = JArray.Parse(json);
			Assert.NotEmpty(users);
			Assert.Contains("\"subject\":", json);
			foreach (JToken u in users)
			{
				Assert.False(string.IsNullOrEmpty((string)u["name"]));
				Assert.NotNull(u["title"]);
				Assert.NotNull(u["groups"]);
			}
		}

		[Fact]
		public void Merge_MatchesEntriesWithoutSubjectByDefaultSubject()
		{
			var existing = JArray.Parse(@"[ { 'name': 'Jane Doe', 'title': 'Keep', 'groups': [] } ]");
			var tenant = JArray.Parse(@"[ { 'subject': 'sportal-JaneDoe', 'name': 'Jane Doe', 'title': 'Other', 'groups': ['G'] } ]");

			JArray merged = TenantUserMapper.Merge(existing, tenant);

			Assert.Single(merged);
			Assert.Equal("Keep", (string)merged[0]["title"]);
			Assert.Equal(new[] { "G" }, merged[0]["groups"].ToObject<string[]>());
		}
	}
}
