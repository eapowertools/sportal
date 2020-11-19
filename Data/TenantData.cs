using System;
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
	}
}
