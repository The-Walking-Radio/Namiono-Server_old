using System;
using System.Collections.Generic;
using System.Globalization;

using Namiono;
using System.Net;
using System.Collections.Specialized;

public sealed class Sendeplan<T> : Provider<T, SendeplanEntry<T>>, IDisposable
{
	public class Sp_settings<T> : IDisposable
	{
		byte startTime;
		byte endTime;
		string topic;
		string autodj_avatar = "images/autodj.gif";

		public byte Start
		{
			get
			{
				return startTime;
			}

			set
			{
				startTime = value;
			}
		}

		public string AutoDJAvatar
		{
			get
			{
				return autodj_avatar;
			}

			set
			{
				autodj_avatar = value;
			}
		}

		public string Topic
		{
			get
			{
				return topic;
			}

			set
			{
				topic = value;
			}
		}

		public byte End
		{
			get
			{
				return endTime;
			}

			set
			{
				endTime = value;
			}
		}

		public Sp_settings(byte start = 18, byte end = 23, string topic = "Querbeet")
		{
			this.startTime = start;
			this.endTime = end;
			this.topic = topic;
		}

		public void Dispose()
		{
		}
	}

	SQLDatabase<T> db;
	Users<T> users;
	Sp_settings<T> settings;
	Filesystem fs;
	Guid spident;
	string name;

	public Sendeplan(Guid guid, string name, ref SQLDatabase<T> db,
		ref Filesystem fs, ref Users<T> users, ref Sp_settings<T> settings)
	{
		this.settings = settings;
		this.name = name;
		this.spident = guid;
		this.fs = fs;
		this.users = users;
		this.db = db;
	}

	public void Start()
	{
	}

	public static DateTime GetMonday()
	{
		var today = (int)DateTime.Now.DayOfWeek;
		return DateTime.Today.AddDays((today == 0) ? -6 : -today + (int)DayOfWeek.Monday);
	}

	int GetEntryCount(int day, double timestamp, byte start, byte end)
	{
		var entryCount = db.SQLQuery<uint>(string.Format("SELECT * FROM sendeplan WHERE day='{0}' AND timestamp >='{1}' AND hour >='{2}' AND hour <='{3}' AND spident ='{4}'",
			day, timestamp, start, end, this.spident));

		var entryCount2 = db.SQLQuery<uint>(string.Format("SELECT * FROM sendeplan_replay WHERE day='{0}' AND hour >='{1}' AND hour <='{2}' AND spident ='{3}'",
		day, start, end, this.spident));

		return entryCount.Count + entryCount2.Count;
	}

	string getSPDay(byte day, ref T userid)
	{
		var curDay = GetMonday().AddDays(day);
		var timestamp = curDay.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
		var tpl = Dispatcher.ReadTemplate(ref fs, "sp_day");

		tpl = tpl.Replace("[#DAY#]", string.Format("{0}", day));
		tpl = tpl.Replace("[#SDATE#]", curDay.ToShortDateString());
		tpl = tpl.Replace("[#DAYNAME#]", curDay.ToString("dddd", new CultureInfo("de-DE")));

		var entries = string.Empty;

		for (var i_time = settings.Start; i_time <= settings.End; i_time++)
			entries += GetSPEntry(ref curDay, day, i_time, timestamp + (i_time * 3600 * 86400), timestamp);

		var entry_counts = GetEntryCount(day, timestamp, settings.Start, settings.End);
		var addLink = string.Empty;

		if (users.CanAddSendeplan(userid))
		{
			if (users.GetModerators().Count != 0)
				if (entry_counts != (settings.End - settings.Start))
				{
					addLink = string.Format("<div class=\"sp_row\"><a href=\"#\" onclick=\"LoadDocument('/providers/sendeplan/'," +
				  "'#content','Sendeplan','add','day={0}&spident={1}', '')\"><p class=\"exclaim\">Eintragen</p></a></div>", day, spident);

					tpl += addLink;
				}
		}

		tpl = tpl.Replace("[#SP_ENTRIES#]", entries);

		return tpl;
	}

	public string Name => this.name;

	public string GetCurWeekPlan(T userid)
	{
		var cw_tmp = string.Empty;
		var nw_tmp = string.Empty;

		for (var i_day = byte.MinValue; i_day < 7; i_day++)
			cw_tmp += getSPDay(i_day, ref userid);

		var cw_box = Dispatcher.ReadTemplate(ref fs, "content-box")
		.Replace("[[box-content]]", cw_tmp)
		.Replace("[[content]]", "sendeplan")
		.Replace("[[box-title]]", this.name);

		for (var i_day = (byte)7; i_day < 14; i_day++)
			nw_tmp += getSPDay(i_day, ref userid);

		var nw_box = Dispatcher.ReadTemplate(ref fs, "content-box")
				.Replace("[[box-content]]", nw_tmp)
				.Replace("[[content]]", "sendeplan")
				.Replace("[[box-title]]", string.Format("{0} - Vorschau", this.name));

		return cw_box += nw_box;
	}

	public string GetSendeBanner()
	{
		var banners = new List<string>();

		for (var i = 0; i < 14; i++)
		{
			var d_ts = GetMonday().AddDays(i).Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
			var dbEntry = db.SQLQuery<uint>(string.Format("SELECT banner FROM sendeplan WHERE spident = '{0}'", this.spident));

			if (dbEntry.Count != 0)
				for (var ib = uint.MinValue; ib < dbEntry.Count; i++)
					if (!string.IsNullOrEmpty(dbEntry[ib]["banner"]))
						if (fs.Exists(Filesystem.Combine("uploads/events/", dbEntry[ib]["banner"])))
							banners.Add(string.Format("<img src=\"uploads/events/{0}\" />", dbEntry[ib]["banner"]));
		}

		return banners.Count != 0 ? banners[new System.Random().Next(0, banners.Count)] : string.Empty;
	}

	Dictionary<uint, NameValueCollection> Get_Replay_Entries(int day, byte hour)
	{
		return db.SQLQuery<uint>(string.Format("SELECT * FROM sendeplan_replay WHERE day='{0}' AND hour='{1}' AND spident='{2}'", day, hour, this.spident));
	}

	Dictionary<uint, NameValueCollection> Get_Entries(int day, byte hour, double ts, double d_ts)
	{
		return db.SQLQuery<uint>(string.Format("SELECT * FROM sendeplan WHERE timestamp >='{0}' AND day='{1}' AND hour='{2}'" +
				" AND timestamp < '{3}' AND spident='{4}'", d_ts, day, hour, ts, this.spident));
	}

	string enumEntry(Dictionary<uint, NameValueCollection> collection, int day, byte hour)
	{
		var entry = Dispatcher.ReadTemplate(ref fs, "sp_entry");

		for (var i = uint.MinValue; i < collection.Count; i++)
		{
			var uid = (T)Convert.ChangeType(uint.Parse(collection[i]["uid"]), typeof(T));

			if (!users.UserExists(uid))
				continue;

			entry = entry.Replace("[#SPID#]", collection[i]["id"]);
			entry = entry.Replace("[#TS#]", collection[i]["timestamp"]);
			entry = entry.Replace("[#AVATAR#]", users.GetUserAvatarbyID(uid));

			var topic = (collection[i]["description"].Length > 32) ?
				collection[i]["description"].Substring(0, 31) : collection[i]["description"];

			entry = entry.Replace("[#TOPIC#]", topic);
			entry = entry.Replace("[#MOD#]", users.GetUserNamebyID(uid));
			entry = entry.Replace("[#UID#]", string.Format("{0}", uid));
			entry = entry.Replace("[#SPIDENT#]", string.Format("{0}", this.spident));

			entry = entry.Replace("[#DAY#]", string.Format("{0}", day));
			entry = entry.Replace("[#HOUR#]", string.Format("{0}", hour));
		}

		return entry;
	}

	string GetSPEntry(ref DateTime week, int day, byte hour, double time, double d_ts)
	{
		var entry = string.Empty;

		var ts = GetMonday().AddDays(day).Subtract(new DateTime(1970, 1, 1)).TotalSeconds + 86400;
		var uid = (T)Convert.ChangeType(db.SQLQuery("SELECT id FROM users WHERE service_profile='1' AND moderator='1' LIMIT '1'", "id"), typeof(T));

		var dbEntry = Get_Entries(day, hour, ts, d_ts);
		if (dbEntry.Count != 0)
			entry = enumEntry(dbEntry, day, hour);
		else
		{
			var dbEntry_replay = Get_Replay_Entries(day, hour);
			if (dbEntry_replay.Count != 0)
				entry = enumEntry(dbEntry_replay, day, hour);
			else
			{
				entry = Dispatcher.ReadTemplate(ref fs, "sp_entry");
				entry = entry.Replace("[#TOPIC#]", "Playlist");
				entry = entry.Replace("[#MOD#]", users.GetUserNamebyID(uid));
				entry = entry.Replace("[#UID#]", string.Format("{0}", uid));
				entry = entry.Replace("[#AVATAR#]", users.GetUserAvatarbyID(uid));
			}

			entry = entry.Replace("[#DAY#]", string.Format("{0}", day));
			entry = entry.Replace("[#HOUR#]", string.Format("{0}", hour));
			entry = entry.Replace("[#TS#]", string.Format("{0}", d_ts));
			entry = entry.Replace("[#SPID#]", string.Format("{0}", 0));
			entry = entry.Replace("[#SPIDENT#]", string.Format("{0}", this.spident));

		}

		return entry;
	}

	public string Edit_Form(int day, byte hour, double timestamp, T userid)
	{
		var sp_entry = db.SQLQuery<uint>(string.Format("SELECT * FROM sendeplan WHERE day='{0}' AND hour='{1}' AND timestamp='{2}' AND spident='{3}' LIMIT '1'",
			day, hour, timestamp, this.spident));

		var output = string.Empty;

		if (sp_entry.Count != 0)
		{
			var tpl = Dispatcher.ReadTemplate(ref fs, "sp_form_edit");
			for (var i = uint.MinValue; i < sp_entry.Count; i++)
			{
				var mods = users.GetModerators();
				if (mods.Count != 0)
				{
					var modEntries = string.Empty;
					for (var i_mod = uint.MinValue; i_mod < mods.Count; i_mod++)
					{
						var t = (T)Convert.ChangeType(i_mod, typeof(T));
						modEntries += string.Format("\t<option value=\"{0}\">{1}</option>",
							mods[t]["id"], mods[t]["username"]);
					}

					tpl = tpl.Replace("[#SP_MODS#]", modEntries);
				}

				tpl = tpl.Replace("[#DAY#]", sp_entry[i]["day"]);
				tpl = tpl.Replace("[#UID#]", sp_entry[i]["uid"]);
				tpl = tpl.Replace("[#TS#]", sp_entry[i]["timestamp"]);
				tpl = tpl.Replace("[#HOUR#]", sp_entry[i]["hour"]);
				tpl = tpl.Replace("[#TOPIC#]", sp_entry[i]["description"]);
				tpl = tpl.Replace("[#SPIDENT#]", this.spident.ToString());

				output = tpl;
			}

			output = output.Replace("[[box-content]]", tpl).Replace("[[box-title]]", "Sendung bearbeiten").Replace("[[content]]", "sendeplan-edit");
		}

		return output;
	}

	public string Add_Form(int day, T userid)
	{
		var week = GetMonday().AddDays(day);
		var day_timestamp = week.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

		var tpl = Dispatcher.ReadTemplate(ref fs, "content_box");
		var title = string.Format("Sendung für {0} eintragen...", week.ToString("dddd", new CultureInfo("de-DE")));
		var addFormTpl = string.Empty;

		if (users.CanAddSendeplan(userid))
		{
			addFormTpl = Dispatcher.ReadTemplate(ref fs, "sp_form_add");

			addFormTpl = addFormTpl.Replace("[#DAY#]", string.Format("{0}", day));
			addFormTpl = addFormTpl.Replace("[#TS#]", string.Format("{0}", day_timestamp));
			addFormTpl = addFormTpl.Replace("[#UID#]", string.Format("{0}", userid));
			addFormTpl = addFormTpl.Replace("[#TOPIC#]", settings.Topic);
			addFormTpl = addFormTpl.Replace("[#SPIDENT#]", this.spident.ToString());

			var hours = db.SQLQuery<uint>(string.Format("SELECT hour FROM sendeplan WHERE day='{0}' AND timestamp >'{1}' and spident='{2}'", day, day_timestamp, this.spident));
			if (hours.Count == 0)
			{
				var hours_replay = db.SQLQuery<uint>(string.Format("SELECT hour FROM sendeplan_replay WHERE day='{0}'", day));
				if (hours_replay.Count == 0)
				{
					var hourEntries = string.Empty;
					for (var i_entry = settings.Start; i_entry <= settings.End; i_entry++)
					{
						var replays = db.SQLQuery<uint>(string.Format("SELECT hour FROM sendeplan_replay WHERE day='{0}' AND hour='{1}'", day, i_entry));
						if (replays.Count == 0)
							hourEntries += string.Format("\t<option value=\"{0}\">{0}:00</option>\n", i_entry);
					}

					addFormTpl = addFormTpl.Replace("[#SP_HOURS#]", hourEntries);
				}
			}
			else
			{
				var hourEntries = string.Empty;
				for (var i_entry = settings.Start; i_entry <= settings.End; i_entry++)
				{
					var hCount = db.SQLQuery<uint>(string.Format("SELECT * FROM sendeplan WHERE day='{0}' AND hour=\"{1}\" AND timestamp > '{2}' AND spident='{3}'",
						day, i_entry, day_timestamp, this.spident));

					if (hCount.Count == 0)
						hourEntries += string.Format("\t<option value=\"{0}\">{0}:00</option>\n", i_entry);
				}

				addFormTpl = addFormTpl.Replace("[#SP_HOURS#]", hourEntries);
			}

			var mods = users.GetModerators();
			if (mods.Count != 0)
			{
				var modEntries = string.Empty;
				for (var i_mod = uint.MinValue; i_mod < mods.Count; i_mod++)
				{
					var t = (T)Convert.ChangeType(i_mod, typeof(T));
					modEntries += string.Format("\t<option value=\"{0}\">{1}</option>",
						mods[t]["id"], mods[t]["username"]);
				}

				addFormTpl = addFormTpl.Replace("[#SP_MODS#]", modEntries);
			}
			else
				addFormTpl = "Keine Moderatoren gefunden!";
		}
		else
			addFormTpl = "Keine Berechtigung!";

		return tpl.Replace("[[box-content]]", addFormTpl).Replace("[[box-title]]", title).Replace("[[content]]", "sendeplan-add");
	}

	public bool CleanupDatabase()
	{
		var users = db.SQLQuery<uint>("SELECT id FROM users WHERE moderator='0'");
		var result = false;

		for (var i = uint.MinValue; i < users.Count; i++)
		{
			if (db.Count("sendeplan", "uid", users[i]["id"]) != 0)
				result = db.SQLInsert(string.Format("DELETE FROM sendeplan WHERE uid='{0}' AND spident='{1}'", users[i]["id"], this.spident));

			if (db.Count("sendeplan_replay", "uid", users[i]["id"]) != 0)
				result = db.SQLInsert(string.Format("DELETE FROM sendeplan_replay WHERE uid='{0}' AND spident='{1}'", users[i]["id"], this.spident));

		}

		if ((int)DateTime.Now.DayOfWeek == 1 && DateTime.Now.Hour == 1 && DateTime.Now.Minute < 2)
		{
			var ts = GetMonday().Subtract(new DateTime(1970, 1, 1)).TotalSeconds + settings.Start * 3600;

			result = db.SQLInsert(string.Format("DELETE FROM sendeplan WHERE timestamp < '{0}' AND spident='{1}'", ts, this.spident));
		}

		return result;
	}

	public bool DeleteSPEntry(double day, byte hour, double timestamp)
	{
		var result = false;

		if (db.Count("sendeplan", "timestamp", string.Format("{0}", timestamp)) != 0)
		{
			result = db.SQLInsert(string.Format("DELETE FROM sendeplan WHERE day='{0}' AND hour='{1}' AND timestamp='{2}' AND spident='{3}'",
			  day, hour, timestamp, this.spident));
		}

		return result;
	}

	public bool Update_SPEntry(string day, string hour, string timestamp, string user, string description, T userid)
	{
		var res = false;
		if (users.CanEditSendeplan(userid) && users.IsModerator((T)Convert.ChangeType(user, typeof(T))))
		{
			res = db.SQLInsert(string.Format("UPDATE sendeplan SET day='{0}' WHERE timestamp='{1}' AND spident='{2}'", day, timestamp, spident));
			res = db.SQLInsert(string.Format("UPDATE sendeplan SET hour='{0}' WHERE timestamp='{1}' AND spident='{2}'", hour, timestamp, spident));
			res = db.SQLInsert(string.Format("UPDATE sendeplan SET uid='{0}' WHERE timestamp='{1}' AND spident='{2}'", user, timestamp, spident));
			res = db.SQLInsert(string.Format("UPDATE sendeplan SET description='{0}' WHERE timestamp='{1}' AND spident='{2}'", description, timestamp, spident));

			return res;
		}
		else
			return res;
	}

	public bool Insert_SPEntry(string day, string hour, string timestamp, string user, string description, T userid)
	{
		var ts = double.Parse(timestamp) + double.Parse(hour) * 3600;
		var t = db.SQLQuery<uint>(string.Format("SELECT * FROm sendeplan WHERE day='{0}' AND hour='{1}' AND timestamp >='{2}' AND spident='{3}'",
			day, hour, ts, this.spident));

		var tmp = false;

		if (t.Count == 0)
		{
			var ismod = db.SQLQuery(string.Format("SELECT moderator FROM users WHERE id='{0}' LIMIT '1'", user), "moderator");
			if (ismod == "1")
			{
				tmp = db.SQLInsert(string.Format("INSERT INTO sendeplan (day,hour,description,uid,timestamp,spident,banner)" +
				"VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','')", day, hour,
				string.IsNullOrEmpty(description) ? settings.Topic : description, user, ts, spident));
			}
		}

		return tmp;
	}

	public void Heartbeat()
	{
		CleanupDatabase();
	}

	public void Close()
	{
	}

	public void Dispose()
	{
		settings.Dispose();
	}

	public Sp_settings<T> Settings
	{
		get
		{
			return settings;
		}
	}

	public override Dictionary<T, SendeplanEntry<T>> Members
	{
		get
		{
			return members;
		}
	}
}

public class SendeplanEntry<T>
{
	public SendeplanEntry()
	{
	}
}
