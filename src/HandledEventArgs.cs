using System;
using System.Collections.Generic;

namespace Zongsoft.Scheduling
{
	public class HandledEventArgs : EventArgs
	{
		#region 成员字段
		private IHandler _handler;
		private IHandlerContext _context;
		#endregion

		#region 构造函数
		public HandledEventArgs(IHandler handler, IHandlerContext context)
		{
			_handler = handler;
			_context = context;
		}
		#endregion

		#region 公共属性
		public IHandler Handler
		{
			get
			{
				return _handler;
			}
		}

		public ITrigger Trigger
		{
			get
			{
				return _context.Trigger;
			}
		}

		public IHandlerContext Context
		{
			get
			{
				return _context;
			}
		}
		#endregion
	}
}
