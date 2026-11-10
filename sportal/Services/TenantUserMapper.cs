using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace sportal.Services
{
	// Pure conversion/merge logic between Qlik Cloud API objects and users.json entries.
	public static class TenantUserMapper
	{
		// The JWT 'groups' claim can only produce IdP groups, so custom groups are left out by default.
		public const bool INCLUDE_CUSTOM_GROUPS = false;

		public static JArray ToUserEntries(IEnumerable<JObject> users, IEnumerable<JObject> groups)
		{
			// group id -> name, for groups that can be reproduced through the JWT claim
			var groupNames = new Dictionary<string, string>();
			foreach (JObject group in groups)
			{
				string id = (string)group["id"];
				string name = (string)group["name"];
				bool isCustom = string.Equals((string)group["providerType"], "custom", StringComparison.OrdinalIgnoreCase);
				if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name) && (INCLUDE_CUSTOM_GROUPS || !isCustom))
				{
					groupNames[id] = name;
				}
			}

			var entries = new JArray();
			foreach (JObject user in users)
			{
				string subject = (string)user["subject"];
				if (string.IsNullOrEmpty(subject))
				{
					continue;
				}
				string email = (string)user["email"];
				string name = (string)user["name"];
				if (string.IsNullOrWhiteSpace(name))
				{
					name = !string.IsNullOrWhiteSpace(email) ? email : subject;
				}

				var userGroups = new JArray();
				if (user["assignedGroups"] is JArray assigned)
				{
					foreach (JToken g in assigned)
					{
						string groupName = ResolveGroupName(g, groupNames);
						if (groupName != null && !userGroups.Any(x => (string)x == groupName))
						{
							userGroups.Add(groupName);
						}
					}
				}

				var userRoles = new JArray();
				if (user["assignedRoles"] is JArray roles)
				{
					foreach (string roleName in roles.Select(r => (string)r["name"]).Where(n => !string.IsNullOrEmpty(n)).Distinct())
					{
						userRoles.Add(roleName);
					}
				}

				// A user can hold many roles, so none is shown as a title; users.json still needs the field.
				var entry = new JObject
				{
					["subject"] = subject,
					["name"] = name,
					["title"] = "",
					["groups"] = userGroups,
					["roles"] = userRoles
				};
				if (!string.IsNullOrEmpty((string)user["id"]))
				{
					entry["id"] = (string)user["id"];
				}
				if (!string.IsNullOrWhiteSpace(email))
				{
					entry["email"] = email;
				}
				entries.Add(entry);
			}
			return entries;
		}

		private static string ResolveGroupName(JToken assignedGroup, Dictionary<string, string> groupNames)
		{
			if (assignedGroup.Type == JTokenType.String)
			{
				return groupNames.TryGetValue((string)assignedGroup, out string byId) ? byId : null;
			}
			string id = (string)assignedGroup["id"];
			if (!string.IsNullOrEmpty(id))
			{
				return groupNames.TryGetValue(id, out string name) ? name : null;
			}
			return null;
		}

		// The subject users.json assigns to an entry (mirrors UserService.GenerateUsers defaults).
		public static string EffectiveSubject(JObject entry)
		{
			if (entry.ContainsKey("subject"))
			{
				return entry["subject"].ToString();
			}
			return "sportal-" + (entry["name"]?.ToString() ?? "").Replace(" ", string.Empty);
		}

		// Tenant values win for name/email/groups; locally authored title/biography/image are kept.
		// Entries that are not in the tenant are kept untouched.
		public static JArray Merge(JArray existing, JArray tenant)
		{
			var merged = new JArray(existing.Select(e => e.DeepClone()));
			var bySubject = new Dictionary<string, JObject>();
			foreach (JObject entry in merged.OfType<JObject>())
			{
				bySubject[EffectiveSubject(entry)] = entry;
			}

			foreach (JObject t in tenant.OfType<JObject>())
			{
				string subject = (string)t["subject"];
				if (bySubject.TryGetValue(subject, out JObject local))
				{
					local["subject"] = subject;
					local["name"] = t["name"];
					if (t.ContainsKey("email"))
					{
						local["email"] = t["email"];
					}
					local["groups"] = t["groups"].DeepClone();
					local["roles"] = t["roles"]?.DeepClone() ?? new JArray();
					if (t.ContainsKey("id"))
					{
						local["id"] = t["id"];
					}
				}
				else
				{
					JObject added = (JObject)t.DeepClone();
					merged.Add(added);
					bySubject[subject] = added;
				}
			}
			return merged;
		}
	}
}
