using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Namiono
{
	public sealed class Users<T> : Provider<T, User<T>>
	{
		SQLDatabase<T> db;

		public Users(ref SQLDatabase<T> db)
		{
			this.db = db;
		}

		public void UpdatePassword(T id, string password)
		{
			if (!string.IsNullOrEmpty(password))
				db.SQLInsert(string.Format("UPDATE users SET password='{1}' WHERE id='{0}' LIMIT '1'", id, MD5.GetMD5Hash(password)));
		}

		public bool UserExists(T id)
		{
			var uid = string.Format("{0}", id);
			return db.Count("users", "id", string.Format("{0}", id)) != 0 && uid != "0";
		}

		public bool CanEditUsers(T id)
		{
			if (UserExists(id))
			{
				var groupid = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", id), "usergroup");

				if (string.IsNullOrEmpty(groupid))
					return false;

				var can = IsAbleToDo(id, "edit_user");
				return GetGroupLevelByID((T)Convert.ChangeType(byte.Parse(groupid), typeof(T))) >= 235 ? true : can;
			}
			else
				return false;
		}

		public bool CanDeleteUsers(T id)
		{
			if (UserExists(id))
			{
				var groupid = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", id), "usergroup");

				if (string.IsNullOrEmpty(groupid))
					return false;

				var can = IsAbleToDo(id, "delete_user");
				return GetGroupLevelByID((T)Convert.ChangeType(byte.Parse(groupid), typeof(T))) >= 235 ? true : can;
			}
			else
				return false;
		}

		public bool IsServiceProfile(T id)
		{
			return !string.IsNullOrEmpty(db.SQLQuery(string.Format("SELECT service_profile FROM users WHERE id='{0}'", id), "service_profile"));
		}

		public bool CanDeleteSendeplan(T id)
		{
			if (UserExists(id))
			{
				var groupid = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", id), "usergroup");

				if (string.IsNullOrEmpty(groupid))
					return false;

				var can = IsAbleToDo(id, "del_sp");
				return GetGroupLevelByID((T)Convert.ChangeType(byte.Parse(groupid), typeof(T))) >= 230 ? true : can;
			}
			else
				return false;
		}

		public bool CanEditSendeplan(T id)
		{
			if (UserExists(id))
			{
				var groupid = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", id), "usergroup");

				if (string.IsNullOrEmpty(groupid))
					return false;

				var can = IsAbleToDo(id, "edit_sp");

				return GetGroupLevelByID((T)Convert.ChangeType(byte.Parse(groupid), typeof(T))) >= 230 ? true : can;
			}
			else
				return false;
		}

		public bool CanSeeStreamStats(T id)
		{
			if (UserExists(id))
			{
				var groupid = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", id), "usergroup");

				if (string.IsNullOrEmpty(groupid))
					return false;

				var can = IsAbleToDo(id, "see_sc_stats");

				return GetGroupLevelByID((T)Convert.ChangeType(byte.Parse(groupid), typeof(T))) >= 225 ? true : can;
			}
			else
				return false;
		}

		public bool CanSeeSiteSettings(T id)
		{
			if (UserExists(id))
			{
				var groupid = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", id), "usergroup");

				if (string.IsNullOrEmpty(groupid))
					return false;

				var can = IsAbleToDo(id, "see_sitesettings");

				return GetGroupLevelByID((T)Convert.ChangeType(byte.Parse(groupid), typeof(T))) >= 225 ? true : can;
			}
			else
				return false;
		}

		public bool CanAddSendeplan(T id)
		{
			if (UserExists(id))
			{
				var groupid = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", id), "usergroup");

				if (string.IsNullOrEmpty(groupid))
					return false;

				var can = IsAbleToDo(id, "add_sp");

				return GetGroupLevelByID((T)Convert.ChangeType(byte.Parse(groupid), typeof(T))) >= 230 ? true : can;
			}
			else
				return false;
		}

		public bool CanCreateUsers(T id)
		{
			if (UserExists(id))
			{
				var groupid = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", id), "usergroup");

				if (string.IsNullOrEmpty(groupid))
					return false;

				var can = IsAbleToDo(id, "create_user");
				return GetGroupLevelByID((T)Convert.ChangeType(byte.Parse(groupid), typeof(T))) >= 235 ? true : can;
			}
			else
				return false;
		}

		public bool CanSeeAdminCenter(T id)
		{
			if (UserExists(id))
			{
				var groupid = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", id), "usergroup");

				if (string.IsNullOrEmpty(groupid))
					return false;

				var can = IsAbleToDo(id, "see_admincp");
				return GetGroupLevelByID((T)Convert.ChangeType(byte.Parse(groupid), typeof(T))) >= 230 ? true : can;
			}
			else
				return false;
		}

		bool IsAbleToDo(T id, string value)
		{
			var can = false;
			var val = can;

			val = bool.TryParse(db.SQLQuery(string.Format("SELECT can_{1} FROM users WHERE id='{0}'"
				, id, value), string.Format("can_{0}", value)), out can);

			return val;
		}

		public string GetGroupNameByLevel(T level)
		{
			return db.SQLQuery(string.Format("SELECT name FROM usergroups WHERE level='{0}'", level), "name");
		}

		public string GetGroupNameByID(T id)
		{
			return db.SQLQuery(string.Format("SELECT name FROM usergroups WHERE id='{0}'", id), "name");
		}

		public byte GetGroupLevelByID(T id)
		{
			var lvl = db.SQLQuery(string.Format("SELECT level FROM usergroups WHERE id='{0}' LIMIT '1'", id), "level");

			return string.IsNullOrEmpty(lvl) ? byte.MinValue : byte.Parse(lvl);
		}

		public bool UpdateUserName(T id, string username)
		{
			if (UserExists(id))
			{
				if (!string.IsNullOrEmpty(username))
					return db.SQLInsert(string.Format("UPDATE users SET username='{1}' WHERE id='{0}' LIMIT '1'", id, username));
				else
					return false;
			}
			else
				return false;
		}

		public Dictionary<T, NameValueCollection> GetModerators(int locked = 0)
		{
			return db.SQLQuery<T>(string.Format("SELECT * FROM users WHERE moderator='1' AND service_profile='0'", locked));
		}

		public string GetUserNamebyID(T id)
		{
			return db.SQLQuery(string.Format("SELECT * FROM users WHERE id='{0}'", id), "username");
		}

		public string GetUserDesignbyID(T id, string design)
		{
			var d = db.SQLQuery(string.Format("SELECT design FROM users WHERE id='{0}'", id), "design");

			return !string.IsNullOrEmpty(d) ? d : design;
		}

		public bool IsModerator(T id)
		{
			return int.Parse(db.SQLQuery(string.Format("SELECT * FROM users WHERE id='{0}' AND service_profile='0'", id), "moderator")) != 0;
		}

		public bool IsService(T id)
		{
			return int.Parse(db.SQLQuery(string.Format("SELECT * FROM users WHERE id='{0}'", id), "service_profile")) != 0;
		}

		public bool IsLocked(T id)
		{
			return int.Parse(db.SQLQuery(string.Format("SELECT * FROM users WHERE id='{0}'", id), "locked")) != 0;
		}

		public int GetUserLevelbyID(T id)
		{
			var uid = string.Format("{0}", id);
			if (uid == "0")
				return 0;

			var x = db.SQLQuery(string.Format("SELECT usergroup FROM users WHERE id='{0}'", uid), "usergroup");

			return (!string.IsNullOrEmpty(x) ? int.Parse(x) : 0);
		}

		public string GetUserAvatarbyID(T id)
		{
			return db.SQLQuery(string.Format("SELECT avatar FROM users WHERE id='{0}'", id), "avatar");
		}

		public override Dictionary<T, User<T>> Members
		{
			get { return members; }
		}

		public bool HandleRegisterRequest(string username, string password, string password_agree)
		{
			var result = false;

			if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(password_agree))
				return result;

			if (username.Length <= 3)
				return result;

			if (password.Length < 6 || password_agree.Length < 6)
				return result;
			
			if (db.Count("users", "username", username) != 0)
				return result;

			if (MD5.GetMD5Hash(password) == MD5.GetMD5Hash(password_agree))
				if (db.SQLInsert(string.Format("INSERT INTO users (username, password) VALUES ('{0}','{1}')", username, MD5.GetMD5Hash(password))))
					result = true;

			return result;
		}

		public string HandleLoginRequest(string username, string password)
		{
			if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
				return "0";

			var pwhash = MD5.GetMD5Hash(password);

			var usrs = db.SQLQuery(string.Format("SELECT id FROM users WHERE password='{0}' AND username='{1}' AND service_profile != '1' LIMIT '1'", pwhash, username), "id");
			return !string.IsNullOrEmpty(usrs) ? usrs : "0";
		}

		public string GetUserInfo(T id, ref Filesystem fs)
		{
			var box = Dispatcher.ReadTemplate(ref fs, "content-box");
			var picture = GetUserAvatarbyID(id);
			var output = "<ul>";
			var group = "userinfo";

			if (fs.Exists(picture))
				output += string.Format("<li class=\"user_image\"><img src=\"{0}\" /></li>", picture);

			var grouplevel = (T)Convert.ChangeType(GetGroupLevelByID((T)Convert.ChangeType(GetUserLevelbyID(id), typeof(T))), typeof(T));

			var nav_links = db.SQLQuery<uint>(string.Format("SELECT * FROM navigation WHERE level <='{0}' AND target ='userinfo'", grouplevel));

			if (nav_links.Count != 0)
				for (var i = uint.MinValue; i < nav_links.Count; i++)
				{
					var url = nav_links[i]["link"].Contains(".") ? nav_links[i]["link"] : string.Format("/providers/{0}/", nav_links[i]["link"]);
					var data = nav_links[i]["data"].Replace("[#UID#]", string.Format("{0}", id));

					output += string.Format("<li><a href=\"#\" onclick=\"LoadDocument('{0}','#content','{1}','{2}','{3}')\">{1}</a></li>",
						url, nav_links[i]["name"], nav_links[i]["action"], data);
				}

			if (CanSeeAdminCenter(id))
				output += string.Format("<li><a href=\"#\" onclick=\"LoadDocument('/providers/admincp/','#content','Verwaltungs Center','show','admincp=yes')\">Admin Panel</a></li>", id);

			output += string.Format("<li><a href=\"#\" onclick=\"LoadDocument('/providers/users/','.userinfo-box','Logout','logout','')\">Abmelden</a></li>", id);
			output += "</ul>";

			var res = box.Replace("[[box-content]]", output).Replace("[[box-title]]", GetUserNamebyID(id)).Replace("[[content]]", "userinfo");
			return res;
		}

		public bool DeleteUser(T uid, T userid)
		{
			var result = false;

			if (db.Count("users", "id", string.Format("{0}", uid)) != 0 && CanDeleteUsers(userid) && !IsService(uid))
			{
				result = db.SQLInsert(string.Format("DELETE FROM users WHERE id='{0}' AND service_profile != '1'", uid, userid));
				result = db.SQLInsert(string.Format("DELETE FROM sendeplan WHERE uid='{0}'", uid, userid));
				result = db.SQLInsert(string.Format("DELETE FROM sendeplan_replay WHERE uid='{0}'", uid, userid));
			}

			return result;
		}

		public string GetRegisterForm(ref Filesystem fs)
		{
			var tpl = "user_register";
			var output = Dispatcher.ReadTemplate(ref fs, tpl);

			return output;
		}

		public string GetUserList( T userid, ref Filesystem fs)
		{
			var box = Dispatcher.ReadTemplate(ref fs, "content-box");

			var usrs = db.SQLQuery<uint>("SELECT * FROM users WHERE service_profile = '0'");
			var tmp = "<div class=\"tr sp_day\"><div class=\"th\">Name</div><div class=\"th\">Gruppe</div><div class=\"th\">Options</div>";

			if (CanCreateUsers(userid))
				tmp += "(<a class=\"exclaim\" href=\"#\" onclick=\"LoadDocument('/providers/users/','#content','Benutzer erstellen','register','','')\">Benutzer erstellen</a>)";

			tmp += "</div>";

			var options = string.Empty;

			for (var i = uint.MinValue; i < usrs.Count; i++)
			{
				options = string.Format("<a href=\"#\" onclick=\"LoadDocument('/providers/users/','#content','Profil','profile','uid={0}','')\">Profil</a>", usrs[i]["id"]);

				if (CanDeleteUsers(userid) && usrs.Count >= 2)
					options += string.Format(" <a href=\"#\" onclick=\"LoadDocument('/providers/users/','#content','Löschen','del','uid={0}','')\">Löschen</a>", usrs[i]["id"]);

				if (CanEditUsers(userid) || usrs[i]["id"] == string.Format("{0}", userid))
					options += string.Format(" <a href=\"#\" onclick=\"LoadDocument('/providers/users/','#content','Bearbeitem','edit','uid={0}','')\">Edit</a>", usrs[i]["id"]);

				tmp += string.Format("<div class=\"ul_row\"><div class=\"td\">{0}</div><div class=\"td\">{1}</div><div class=\"td\">{2}</div></div>\n",
					usrs[i]["username"], GetGroupNameByID((T)Convert.ChangeType(usrs[i]["usergroup"], typeof(T))), options);
			}

			return box.Replace("[[box-content]]", tmp).Replace("[[box-title]]", "Benutzer Liste").Replace("[[content]]", "userinfo");
		}

		public string GetProfile(ref Filesystem fs, T id)
		{
			var output = string.Empty;

			if (UserExists(id))
			{
				output = Dispatcher.ReadTemplate(ref fs, "users_profile");

				var level = (T)Convert.ChangeType(GetUserLevelbyID(id), typeof(T));
				output = output.Replace("[#USER_NAME#]", GetUserNamebyID(id));
				output = output.Replace("[#USER_LEVEL#]", string.Format("{0}", GetGroupNameByID(level)));
				output = output.Replace("[#USER_ID#]", string.Format("{0}", id));
			}

			return output;
		}

		public string GetLoginForm(ref Filesystem fs)
		{
			var tpl = "user_login";
			var output = Dispatcher.ReadTemplate(ref fs, tpl);

			return output;
		}

		public string Get_admincp_user_settings(Filesystem fs, T user)
		{
			var output = Dispatcher.ReadTemplate(ref fs, "user_admin_settings");

			return output;
		}

		public void Close()
		{
			foreach (var item in members.Values)
				item?.Close();
		}

		public void Start()
		{
			foreach (var item in members.Values)
				item?.Start();
		}

		public void HeartBeat()
		{
			foreach (var item in members.Values)
				item?.Heartbeat();
		}

		public void Update()
		{
			foreach (var item in members.Values)
				item?.Update();
		}

		public void Dispose()
		{
			foreach (var item in members.Values)
				item?.Dispose();
		}
	}

	public class User<T> : Member<T>
	{
		string username;
		string password;
		uint userlevel;

		public override string OutPut => string.Empty;

		public override void Close()
		{
		}

		public override void Heartbeat()
		{
		}

		public override void Start()
		{
		}

		public override void Update()
		{
		}

		public string UserName
		{
			get { return username; }
			set { username = value; }
		}

		public string Password
		{
			get { return password; }
			set { password = value; }
		}

		public uint UserLevel
		{
			get { return userlevel; }
			set { userlevel = value; }
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
	}
}
