using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Xml;
using MaxMind.GeoIP2;
using System.Threading.Tasks;

namespace Namiono
{
	public sealed class ShoutcastServer<T> : Provider<T, ShoutcastStream<T>>
	{
		Uri scURL;
		ushort port;
		string password;
		string hostname;
		bool advertXMLStats;

		Users<T> users;
		DatabaseReader geodb;
		SQLDatabase<T> db;
		Filesystem fs;
		TranscastClients<T> autodj;
		string serverid;
		string uaid;

		public enum ServerType
		{
			Legacy,
			Shoutcast2,
			Transcast
		}

		public event ShoutcastStartedEventHandler ShoutcastStarted;
		public delegate void ShoutcastStartedEventHandler(object sender, ShoutcastStartedEventArgs e);
		public class ShoutcastStartedEventArgs : EventArgs
		{
			public string Message;

			public ShoutcastStartedEventArgs(string message)
			{
				this.Message = message;
			}
		}

		public ShoutcastServer(ref SQLDatabase<T> db, ref Filesystem fs, ref Users<T> users, string serverid,
			string hostname, ushort port, string password, bool isStateServer, bool transcastNeeded)
		{
			this.db = db;
			this.uaid = Guid.NewGuid().ToString();

			if (transcastNeeded)
			{
				var tc = this.db.SQLQuery<uint>("SELECT relay_host, relay_port, relay_admin, relay_pass FROM shoutcast");
				if (tc.Count != 0)
				{
					for (var i = uint.MinValue; i < tc.Count; i++)
					{
						autodj = new TranscastClients<T>(tc[i]["relay_host"],
							ushort.Parse(tc[i]["relay_port"]), tc[i]["relay_pass"], tc[i]["relay_admin"]);
						Download(ref autodj);
					}
				}
			}

			this.serverid = serverid;
			this.advertXMLStats = isStateServer;
			this.hostname = hostname;
			this.password = password;
			this.port = port;
			this.users = users;
			this.fs = fs;

			this.geodb = new DatabaseReader(fs.ResolvePath("uploads/GeoLite2-City.mmdb"));

			var scurl = string.Format("http://{0}:{1}/statistics", this.hostname, this.port);
			scURL = new Uri(scurl);
		}

		public string Webplayer(T id)
		{
			var player_output = "<audio controls=\"\" preload=\"none\" muted=\"yes\" class=\"player_control\">\n";
			var strPath = members[id].StreamPath;
			var ctype = members[id].ContentType;

			player_output += string.Format("<source src=\"{0}\" type=\"{1}\" />\n",
				strPath, ctype);

			player_output += "Not supported!</audio>\n";

			return player_output;
		}

		public bool IsStateServer => advertXMLStats;

		public string OutPut(T userid)
		{
			var playerOutput = "<p class=\"exclaim\">Keine Streams gefunden!</p>\n";

			if (members.Count != 0)
			{
				foreach (var member in members.Values)
				{
					if (member == null)
						continue;

					if (!member.Active)
						continue;

					playerOutput = member.MetaData;
					playerOutput += "<hr />";

					var nav_links = db.SQLQuery<uint>("SELECT * FROM navigation WHERE target ='playlist'");
					playerOutput += "<ul>";

					if (nav_links.Count != 0)
					{
						for (var i = uint.MinValue; i < nav_links.Count; i++)
						{
							var data = nav_links[i]["data"]
								.Replace("[#STREAMID#]", string.Format("{0}", member.ID))
								.Replace("[#SERVERID#]", string.Format("{0}", this.serverid));

							var url = string.Format("/providers/{0}/?{1}", nav_links[i]["link"], data);
							playerOutput += string.Format("<li><a href=\"#\" onclick=\"Window('{0}','{1}')\">{1}</a></li>", url, nav_links[i]["name"]);
						}
					}

					if (users.CanSeeStreamStats(userid))
						playerOutput += string.Format("\t<li><a href=\"#\" onclick=\"LoadDocument('/providers/shoutcast/','#content','{0}'," +
							"'clients','serverid={1}&sid={2}&show=clients', '')\">Aktuelle Zuhörer</a></li>", member.ServerName, this.serverid, member.ID);

					playerOutput += "</ul>";
				}
			}

			return playerOutput;
		}

		public override Dictionary<T, ShoutcastStream<T>> Members => members;

		void Download(ref TranscastClients<T> source)
		{
			using (var wc = new WebClient())
			{
				wc.DownloadStringCompleted += (wc_sender, wc_e) =>
				{
					try
					{
						if (wc_e.Cancelled)
							return;

						var result = wc_e.Result;
						if (!string.IsNullOrEmpty(result))
							ParseData(result);
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex.Message);
					}
				};

				wc.Headers.Add("Accept", "text/xml");
				wc.Headers.Add("User-Agent", string.Format("Namiono - DNAS Client ({0})", this.uaid));

				wc.Encoding = Encoding.UTF8;
				wc.DownloadStringAsync(scURL);

				if (source != null)
					source.Download();
			}
		}

		void ParseData<X>(X data)
		{
			using (var bgw = new BackgroundWorker())
			{
				bgw.WorkerReportsProgress = true;
				bgw.WorkerSupportsCancellation = true;
				bgw.RunWorkerAsync(data);

				bgw.DoWork += (bgw_doWorkSender, bgw_e) =>
				{
					var response = new XmlDocument();
					response.LoadXml((string)bgw_e.Argument);

					var totalStreams = byte.Parse(response.DocumentElement.
						SelectSingleNode("STREAMSTATS/ACTIVESTREAMS").InnerText);

					if (totalStreams != byte.MinValue && totalStreams < byte.MaxValue)
						for (var i = (byte)1; i <= totalStreams; i++)
						{
							var id = (T)Convert.ChangeType(i, typeof(T));
							lock (members)
							{
								if (members.ContainsKey(id))
									continue;

								var stream = new ShoutcastStream<T>(ref fs, ref geodb, ref users, hostname, port, password);
								stream.ID = id;

								if (members.ContainsKey(stream.ID))
									continue;

								if (!string.IsNullOrEmpty(stream.Clients((T)Convert.ChangeType(users.GetUserIDbyName("Auto DJ"), typeof(T)))))
								{
									if (!members.ContainsKey(stream.ID))
										members.Add(stream.ID, stream);

									members[stream.ID]?.Update();
								}
							}
						}
				};

				bgw.RunWorkerCompleted += (bgw_done_sender, done_e) =>
				{
					lock (members)
					{
						foreach (var stream in members.Values)
							if (stream.Active)
								stream?.Update();
					}
				};
			}
		}

		public string ServerID => this.serverid;

		public void Close()
		{
			lock (members)
			{
				foreach (var member in members.Values)
					member?.Close();
			}

			lock (autodj.Members)
			{
				foreach (var item in autodj.Members.Values)
					item.Close();
			}

			autodj.Members.Clear();
			members.Clear();
		}

		public string ListCurrentStreams()
		{
			var box = Dispatcher.ReadTemplate(ref fs, "content_box");

			var tmp = "<div class=\"tr sp_day\"><div class=\"th sc_col_id\">Addresse</div><div class=\"th sc_col_id\">Modus</div>" +
				"<div class=\"th sc_col_id\">\"Statistic Relay\"-Rolle</div><div class=\"th sc_col_id\">Optionen</div></div>";

			using (var db = new SQLDatabase<T>())
			{
				var streams = db.SQLQuery<uint>("SELECT * FROM shoutcast");
				for (var i = uint.MinValue; i < streams.Count; i++)
				{
					tmp += string.Format("<div class=\"tr sp_row\"><div class=\"td sc_col_id\"><a href=\"#\"onclick=\"Window('http://{0}:{1}/admin.cgi', 'Shoutcast Server')\">{0}</a></div><div class=\"td sc_col_id\">Shoutcast2 & Proxy</div>" +
						"<div class=\"td sc_col_id\">{2}</div><div class=\"td sc_col_id\"><a href=\"#\" onclick=\"LoadDocument('/providers/shoutcast/','#content','Starten'," +
							"'execute','serverid={1}&sid={2}&param=start', '')\">start</a> - <a href=\"#\" onclick=\"LoadDocument('/providers/shoutcast/','#content','Stoppen'," +
							"'execute','serverid={1}&sid={2}&param=stop', '')\">stop</a></div> - </div>", streams[i]["hostname"], streams[i]["port"], this.advertXMLStats ? "Proxy" : "Client");
				}
			}

			return box.Replace("[[box-content]]", tmp).Replace("[[box-title]]", "Shoutcast Streams").Replace("[[content]]", "sc_streamcenter");
		}

		public void Dispose()
		{
			lock (members)
			{
				foreach (var member in members.Values)
					member?.Dispose();
			}

			geodb.Dispose();

			lock (autodj.Members)
			{
				foreach (var item in autodj.Members.Values)
					item.Dispose();
			}
		}

		public void Heartbeat()
		{
			Download(ref autodj);

			lock (members)
			{
				if (members.Count == 0)
					return;

				foreach (var member in members.Values)
					member?.Heartbeat();
			}
		}

		public void Start()
		{
			lock (members)
			{
				foreach (var member in members.Values)
					member?.Start();
			}

			var evArgs = new ShoutcastStartedEventArgs(string.Format("Shoutcast listener gestartet ({0}:{1})", hostname, port));
			ShoutcastStarted?.Invoke(this, evArgs);
		}

		public void Update()
		{
			lock (members)
			{
				foreach (var member in members.Values)
					member?.Update();
			}
		}
	}
}
