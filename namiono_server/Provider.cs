using System.Collections.Generic;

namespace Namiono
{
	public abstract class Provider<T, I>
	{
		protected Dictionary<T, I> members = new Dictionary<T, I>();

		public void Add(T id, I member)
		{
			if (!Contains(id))
				members.Add(id, member);
		}

		public void Remove(T id)
		{
			if (Contains(id))
				members.Remove(id);
		}

		public bool Contains(T id)
		{
			return members.ContainsKey(id);
		}

		public abstract Dictionary<T, I> Members
		{
			get;
		}
	}
}
