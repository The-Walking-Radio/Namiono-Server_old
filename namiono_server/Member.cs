using System;

namespace Namiono
{
	public abstract class Member<T> : IDisposable
	{
		protected T id;
		protected string output;

		public void Dispose()
		{
		}

		public abstract void Start();
		public abstract void Close();
		public abstract void Update();
		public abstract void Heartbeat();

		public abstract string OutPut
		{
			get;
		}

		public abstract T ID
		{
			get; set;
		}
	}
}
