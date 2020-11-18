using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace sportal.Data
{
	public class TenantData
	{
		public string Hostname { get; set; }

		public string WebIntegrationID { get; set; }

		public X509Certificate2 Certificate { get; set; }

		public RSA PrivateKey { get; set; }

		public TenantData()
		{
		}
	}
}
