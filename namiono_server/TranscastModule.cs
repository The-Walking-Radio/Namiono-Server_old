using System;
using System.Collections.Generic;
using System.Net;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Namiono
{
	public class TranscastClients<T> : Provider<T, TranscastSource<T>>
	{
		ushort port;
		string hostname;
		string password;
		string username;
		CredentialCache credentials;

		public override Dictionary<T, TranscastSource<T>> Members
			=> new Dictionary<T, TranscastSource<T>>();

		public TranscastClients(string hostname, ushort port, string password, string username)
		{
			this.hostname = hostname;
			this.password = password;
			this.port = port;
			this.username = username;
			this.credentials = new CredentialCache();
			this.credentials.Add(new Uri(string.Format("http://{0}:{1}/api", this.hostname, this.port)), "Basic",
				new NetworkCredential(this.username, this.password));

			Download();
		}

		public void Download()
		{
			var postdata = "op=test";
			postdata += "&seq=12";

			var req = WebRequest.CreateHttp(new Uri(string.Format("http://{0}:{1}/api", hostname, port)));
			req.UseDefaultCredentials = true;
			req.Credentials = credentials;
			req.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
			req.UserAgent = "Mozilla 5.0";
			req.AllowAutoRedirect = true;
			req.Method = "POST";

			var reqstream = req.GetRequestStream();
			if (reqstream == null)
				return;

			var data = Encoding.UTF8.GetBytes(postdata);
			reqstream.Write(data, 0, data.Length);
			reqstream.Close();

			using (var resp = (HttpWebResponse)req.GetResponse())
			{
				var sr = new StreamReader(resp.GetResponseStream());
				Console.WriteLine(sr.ReadToEnd());
				sr.Close();
			}
		}
	}

	public class TranscastSource<T> : Member<T>
	{
		public override string OutPut => throw new NotImplementedException();

		public override T ID { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public override void Close()
		{
			throw new NotImplementedException();
		}

		public override void Heartbeat()
		{
			throw new NotImplementedException();
		}

		public override void Start()
		{
			throw new NotImplementedException();
		}

		public override void Update()
		{
			throw new NotImplementedException();
		}
	}
}
