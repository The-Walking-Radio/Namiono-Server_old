using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Xml;
using MaxMind.GeoIP2;
using System.Linq;
using System.Globalization;

namespace Namiono
{
	public sealed class ShoutcastStream<T> : Member<T>
	{
		string songTitle;
		string password;
		string serverName;
		string streamPath;
		string serverUrl;
		string contentType;
		Dictionary<T, Listener<T>> listeners;
		Filesystem fs;
		Users<T> users;

		int port;
		string hostname;
		string version;

		XmlDocument xmldoc;
		bool active;
		DatabaseReader geodb;

		public ShoutcastStream(ref Filesystem fs, ref DatabaseReader geodb, ref Users<T> users, string hostname, int port, string password = "")
		{
			this.password = password;
			this.hostname = hostname;
			this.port = port;
			this.fs = fs;
			this.users = users;
			this.geodb = geodb;
			this.version = string.Empty;
			this.listeners = new Dictionary<T, Listener<T>>();
		}

		void ParseData(string response)
		{
			var result = response;
			if (string.IsNullOrEmpty(response))
				return;

			var xml = new XmlDocument();
			xml.LoadXml(result);

			xml.CreateXmlDeclaration("1.0", "UTF-8", "yes");
			serverName = xml.DocumentElement.SelectSingleNode("SERVERTITLE").InnerText;
			serverUrl = xml.DocumentElement.SelectSingleNode("SERVERURL").InnerText;
			songTitle = xml.DocumentElement.SelectSingleNode("SONGTITLE").InnerText;
			contentType = xml.DocumentElement.SelectSingleNode("CONTENT").InnerText;

			streamPath = string.Format("http://{0}:{1}{2}", this.hostname, this.port,
				xml.DocumentElement.SelectSingleNode("STREAMPATH").InnerText);

			active = int.Parse(xml.DocumentElement.SelectSingleNode("STREAMSTATUS").InnerText) != 0;

			lock (listeners)
			{
				listeners.Clear();

				var ls = xml.DocumentElement.SelectNodes("LISTENERS/LISTENER");
				for (var i = 0; i < ls.Count; i++)
				{
					var uid = ulong.Parse(ls.Item(i).SelectSingleNode("UID").InnerText);
					if (!listeners.ContainsKey((T)Convert.ChangeType(uid, typeof(T))))
					{
						var listener = new Listener<T>();
						listener.IP = Dns.GetHostEntry(ls.Item(i).SelectSingleNode("HOSTNAME").InnerText).AddressList[0];

						var ctime = ulong.Parse(ls.Item(i).SelectSingleNode("CONNECTTIME").InnerText);
						listener.ConnectionTime = (T)Convert.ChangeType(ctime, typeof(T));

						var grid = ulong.Parse(ls.Item(i).SelectSingleNode("GRID").InnerText);
						listener.GRID = (T)Convert.ChangeType(grid, typeof(T));

						var triggers = ulong.Parse(ls.Item(i).SelectSingleNode("TRIGGERS").InnerText);
						listener.Triggers = (T)Convert.ChangeType(triggers, typeof(T));

						var ua = ls.Item(i).SelectSingleNode("USERAGENT").InnerText;
						listener.UserAgent = !string.IsNullOrEmpty(ua) ? ua : "Unbekannt";

						var type = ulong.Parse(ls.Item(i).SelectSingleNode("TYPE").InnerText);
						listener.Type = (T)Convert.ChangeType(type, typeof(T));

						listener.UID = (T)Convert.ChangeType(uid, typeof(T));
						listener.RREFERES = ls.Item(i).SelectSingleNode("REFERER").InnerText;

						listener.City = geodb.City(listener.IP).City.Name;
						listener.Country = geodb.City(listener.IP).Country.Name;

						listeners.Add((T)Convert.ChangeType(uid, typeof(T)), listener);
					}
				}
			}
		}

		void Download()
		{
			if (string.IsNullOrEmpty(password))
				return;

			using (var wc = new WebClient())
			{
				wc.DownloadStringCompleted += (sender, e) =>
				{
					try
					{
						ParseData(e.Result);
					}
					catch (Exception ex)
					{
						Console.WriteLine(e.Error);
					}
				};

				wc.DownloadStringAsync(new Uri(string.Format("http://{0}:{1}/admin.cgi?mode=viewxml&pass={2}&sid={3}", hostname, port, password, id)));
			}
		}

		public string MetaData
		{
			get
			{
				var output = string.Format("<ul id=\"playerinfo_{0}\">", id);
				output += string.Format("\t<li><img src=\"images/autodj.gif\" height=\"64px\" class=\"user_image\" /></li>");
				output += string.Format("\t<li class=\"metadata\"><p>{0}</p></li>", songTitle);
				output += "</ul>";

				return output;
			}
		}

		public string ContentType
		{
			get
			{
				return contentType;
			}

			private set
			{
				contentType = value;
			}
		}

		public string StreamPath
		{
			get
			{
				return streamPath;
			}

			private set
			{
				streamPath = value;
			}
		}

		public string ServerURL
		{
			get
			{
				return serverUrl;
			}

			private set
			{
				serverUrl = value;
			}
		}

		public bool Active
		{
			get
			{
				return active;
			}

			set
			{
				active = value;
			}
		}

		public string Clients(T userid)
		{
				var box = Dispatcher.ReadTemplate(ref fs, "content_box");

				var tmp = "<div class=\"tr sp_day\"><div class=\"th  sc_col_id\">Dauer (HH:mm)</div>" +
					"<div class=\"th sc_col_ip\">IP</div><div class=\"th sc_col_ua\">Player</div></div>";

				lock (listeners)
				{
					if (listeners.Count != 0)
					{
						foreach (var l in listeners.Values)
						{
							var ctime = Convert.ToInt32((T)Convert.ChangeType(l.ConnectionTime, typeof(T)));
							var ts = new DateTime(new TimeSpan(ctime / 3600, ctime / 60, ctime / 60 / 60).Ticks).ToString("HH:mm", new CultureInfo("de-DE"));

							tmp += string.Format("<div class=\"tr sp_row\"><div class=\"td sc_col_id\"><h3>{0}</h3></div>" +
								"<div class=\"td sc_col_ip\"><h3>{1}<br />({3}, {4})</h3></div><div class=\"td sc_col_ua\">{2}</div></div>",
								  ts, users.CanSeeStreamStats(userid) ? l.IP : IPAddress.None, l.UserAgent, l.Country, l.City);
						}
					}
					else
						tmp += "<div class=\"tr sp_row\"><p class=\"exclaim\">Zur Zeit keine Zuhörer verfügbar.</p></div>";
				}

				return box.Replace("[[box-content]]", tmp).Replace("[[box-title]]", "Aktive Zuhörer").Replace("[[content]]", "sc_listeners");
		}

		public int Listeners
		{
			get
			{
				return listeners.Count;
			}
		}

		public string SongTitle
		{
			get
			{
				return songTitle;
			}

			private set
			{
				songTitle = value;
			}
		}

		public string ServerName
		{
			get
			{
				return serverName;
			}

			private set
			{
				serverName = value;
			}
		}

		public XmlDocument XML
		{
			get
			{
				return xmldoc;
			}

			set
			{
				xmldoc = value;
			}
		}

		public override string OutPut
		{
			get
			{
				return output;
			}
		}

		public override T ID
		{
			get
			{
				return id;
			}

			set
			{
				id = value;
			}
		}

		public override void Start()
		{
			Update();
		}

		public override void Close()
		{
			listeners.Clear();
		}

		public override void Update()
		{
			var x = new Thread(Download);
			x.IsBackground = true;
			x.Start();
		}

		public override void Heartbeat()
		{
			Update();
		}
	}

	public struct Listener<T>
	{
		IPAddress ipaddress;

		string useragent;
		string referer;

		string city;
		string country;

		T connectionTime;
		T uid;
		T type;
		T grid;
		T triggers;

		public IPAddress IP
		{
			get { return ipaddress; }
			set { ipaddress = value; }
		}

		public string Country
		{
			get
			{
				return country;
			}

			set
			{
				country = value;
			}
		}

		public string City
		{
			get
			{
				return city;
			}

			set
			{
				city = value;
			}
		}

		public string UserAgent
		{
			get
			{
				return useragent;
			}

			set
			{
				if (value.StartsWith("NSPlayer"))
					useragent = "Windows Media Player";
				else
				{
					if (value.StartsWith("Winamp"))
						useragent = "Winamp";
					else
					{
						if (value.StartsWith("BASS") || value.StartsWith("VLC"))
							useragent = "VLC Player";
						else
						{
							if (value.StartsWith("iTunes"))
								useragent = "iTunes";
							else
							{
								if (value.StartsWith("AppleCoreMedia"))
									useragent = value.Contains("(iPhone") ?
									"ITunes (Iphone)" : value;
								else
								{
									if (value.StartsWith("Mozilla/5.0"))
									{
										if (value.Contains("Microsoft Edge"))
											useragent = "Microsoft Edge";

										if (value.Contains("Trident"))
											useragent = "Internet Explorer";

										if (value.Contains("Firefox"))
											useragent = "Mozilla Firefox";

										if (value.Contains("Chrome"))
											useragent = "Google Chrome";

									}
									else
										useragent = value;
								}
							}
						}
					}
				}
			}
		}

		public T ConnectionTime
		{
			get { return connectionTime; }
			set { connectionTime = value; }
		}

		public T UID
		{
			get { return uid; }
			set { uid = value; }
		}

		public T GRID
		{
			get { return grid; }
			set { grid = value; }
		}

		public T Type
		{
			get { return type; }
			set { type = value; }
		}

		public string RREFERES
		{
			get { return referer; }
			set { referer = value; }
		}

		public T Triggers
		{
			get { return triggers; }
			set { triggers = value; }
		}
	}
}
