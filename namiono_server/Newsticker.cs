using Namiono;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace namiono
{
	public class Newsticker<T>
	{
		SQLDatabase<T> db;
		Filesystem fs;

		public Newsticker(ref Filesystem fs, ref SQLDatabase<T> db)
		{
			this.fs = fs;
			this.db = db;
		}

		public string GetNews()
		{
			var result = string.Empty;

			var sql_query = db.SQLQuery<T>("SELECT * FROM newsticker");
			if (sql_query.Count != 0)
			{
				for (var i = 0; i < sql_query.Count; i++)
				{
					var id = sql_query[(T)Convert.ChangeType(i, typeof(T))]["id"];
					var title = sql_query[(T)Convert.ChangeType(i, typeof(T))]["title"];
					var content = sql_query[(T)Convert.ChangeType(i, typeof(T))]["content"];
					var author = sql_query[(T)Convert.ChangeType(i, typeof(T))]["author"];
					var tpl = Dispatcher.ReadTemplate(ref fs, "news-content")
							.Replace("[#ID#]", id)
								.Replace("[#NEWS_TITLE#]", title)
									.Replace("[#NEWS_CONTENT#]", content);

					result += tpl;
				}
			}

			return result;
		}

		public void Close()
		{

		}

		public void Dispose()
		{

		}
	}
}
