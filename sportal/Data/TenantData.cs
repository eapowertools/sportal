using System;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace sportal.Data
{
	[Serializable]
	public class TenantData
	{
		public string Hostname { get; set; }

		public string WebIntegrationID { get; set; }

		public string Issuer { get; set; }

		public string KeyID { get; set; }

		public byte[] Certificate { get; set; }

		[JsonConverter(typeof(JsonStringEnumConverter))]
		public SportalMode Mode { get; set; }

		// The TenantAdmin that Impersonate and Sync modes sign in as; the subject is the tenant's user 'subject'.
		public string AdminSubject { get; set; }

		public string AdminName { get; set; }

		public string AdminEmail { get; set; }
	}
}
