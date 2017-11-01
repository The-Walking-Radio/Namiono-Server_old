using System;
using System.Collections.Generic;
using System.Net;

using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namiono
{
	public class TranscastClients<T> : Provider<T, TranscastSource<T>>
	{
		ushort port;
		string hostname;
		string password;
		string username;

		string title;
		string name;

		public override Dictionary<T, TranscastSource<T>> Members => new Dictionary<T, TranscastSource<T>>();

		public TranscastClients(string hostname, ushort port, string password, string username)
		{
			this.hostname = hostname;
			this.password = password;
			this.port = port;
			this.username = username;
		}

		public void Download()
		{
			var req = WebRequest.CreateHttp(new Uri(string.Format("http://{2}:{3}@{0}:{1}/api", hostname, port, this.password, this.username)));

				req.UserAgent = "Mozilla/5.0";
				req.KeepAlive = true;
				req.AllowAutoRedirect = true;
				req.Accept = "*/*";
				req.Method = "POST";
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
