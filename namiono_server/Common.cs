using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Namiono
{
	public class Common<T>
	{
		Dictionary<string, WebSite<T>> webSites;
		Dictionary<string, ShoutcastServer<T>> ShoutcastServers;

		SQLDatabase<T> db;

		Thread heartbeat;
		bool running;

		public Common(string[] args)
		{
			webSites = new Dictionary<string, WebSite<T>>();
			ShoutcastServers = new Dictionary<string, ShoutcastServer<T>>();
			db = new SQLDatabase<T>();

			var websites = db.SQLQuery<uint>("SELECT * FROM websites");
			if (websites.Count != 0)
			{
				for (var i = uint.MinValue; i < websites.Count; i++)
					AddWebSite(websites[i]["name"], websites[i]["title"], ushort.Parse(websites[i]["port"]),
						(Realm)ushort.Parse(websites[i]["realm"]), ref db);
			}

			running = true;
			heartbeat = new Thread(new ParameterizedThreadStart(Heartbeat))
			{
				IsBackground = true
			};
		}

		void Heartbeat(object interval)
		{
			var timer = ((int)interval * 1000);
			while (running)
			{
				Thread.Sleep(timer);

				foreach (var ws in webSites.Values)
					ws?.HeartBeat();
			}
		}

		public void AddWebSite(string name, string title, ushort port, Realm realm, ref SQLDatabase<T> db)
		{
			if (string.IsNullOrEmpty(name))
				throw new ArgumentException("A WebSite must have a unique name!");

			if (!webSites.ContainsKey(name))
				webSites.Add(name, new WebSite<T>(ref db, name, title, port, realm));
		}

		public void RemoveWebSite(string name)
		{
			if (webSites.ContainsKey(name))
			{
				webSites[name]?.Close();
				webSites.Remove(name);
			}
		}

		public void Start()
		{
			foreach (var ws in webSites.Values)
				ws?.Start();

			heartbeat.Start(45);
		}

		public void Close(bool remove = true)
		{
			running = false;

			foreach (var ws in webSites)
			{
				ws.Value?.Close();

				if (remove)
					webSites.Remove(ws.Key);
			}

			db.Close();
		}

		public void Dispose()
		{
			foreach (var ws in webSites.Values)
				ws?.Dispose();

			webSites.Clear();

			db.Dispose();
		}
	}
}
