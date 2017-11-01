using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SQLite;

namespace Namiono
{
	public sealed class SQLDatabase<T> : IDisposable
	{
		SQLiteConnection sqlConn;

		public SQLDatabase(string database = "database.sqlite")
		{
			var dataBase = Filesystem.Combine(Environment.CurrentDirectory, database);
			sqlConn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", dataBase));

			sqlConn.Open();
		}

		public int Count(string table, string condition, string value)
		{
			using (var cmd = new SQLiteCommand(string.Format("SELECT Count({0}) FROM {1} WHERE {0}='{2}'",
				condition, table, value), sqlConn))
			{
				cmd.CommandType = CommandType.Text;
				return Convert.ToInt32(cmd.ExecuteScalar());
			}
		}

		public Dictionary<T, NameValueCollection> SQLQuery<T>(string sql, CommandType commandType = CommandType.Text)
		{
			var result = (Dictionary<T, NameValueCollection>)null;
			using (var cmd = new SQLiteCommand(sql, sqlConn))
			{
				cmd.CommandType = commandType;
				var res = cmd.ExecuteNonQuery();

				result = new Dictionary<T, NameValueCollection>();
				var reader = cmd.ExecuteReader();
				var i = uint.MinValue;

				while (reader.Read())
					if (!result.ContainsKey((T)Convert.ChangeType(i, typeof(T))))
					{
						result.Add((T)Convert.ChangeType(i, typeof(T)), reader.GetValues());
						i++;
					}

				reader.Close();
			}

			return result;
		}

		public bool SQLInsert(string sql)
		{
			using (var cmd = new SQLiteCommand(sql, sqlConn))
			{
				cmd.ExecuteNonQuery();
				return true;
			}
		}

		public string SQLQuery(string sql, string key)
		{
			var result = string.Empty;
			using (var cmd = new SQLiteCommand(sql, sqlConn))
			{
				cmd.CommandType = CommandType.Text;
				var res = cmd.ExecuteNonQuery();

				var reader = cmd.ExecuteReader();

				while (reader.Read())
					result = string.Format("{0}", reader[key]);

				reader.Close();
			}

			return result;
		}

		public void Close()
		{
			sqlConn.Close();
		}

		public void HeartBeat()
		{
			sqlConn.ReleaseMemory();
		}

		public void Dispose()
		{
			sqlConn.Dispose();
		}
	}
}
