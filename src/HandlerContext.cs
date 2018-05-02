/*
 *   _____                                ______
 *  /_   /  ____  ____  ____  _________  / __/ /_
 *    / /  / __ \/ __ \/ __ \/ ___/ __ \/ /_/ __/
 *   / /__/ /_/ / / / / /_/ /\_ \/ /_/ / __/ /_
 *  /____/\____/_/ /_/\__  /____/\____/_/  \__/
 *                   /____/
 *
 * Authors:
 *   钟峰(Popeye Zhong) <zongsoft@qq.com>
 *
 * Copyright (C) 2018 Zongsoft Corporation <http://www.zongsoft.com>
 *
 * This file is part of Zongsoft.Scheduling.
 *
 * Zongsoft.Scheduling is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * Zongsoft.Scheduling is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with Zongsoft.Scheduling; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA
 */

using System;
using System.Collections.Generic;

namespace Zongsoft.Scheduling
{
	public class HandlerContext : IHandlerContext
	{
		#region 成员字段
		private IDictionary<string, object> _parameters;
		#endregion

		#region 构造函数
		public HandlerContext(IScheduler scheduler, ITrigger trigger, int index)
		{
			this.Index = index;
			this.Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
			this.Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
		}
		#endregion

		#region 公共属性
		public int Index
		{
			get;
		}

		public HandlerFailure? Failure
		{
			get;
			set;
		}

		public IScheduler Scheduler
		{
			get;
		}

		public ITrigger Trigger
		{
			get;
		}

		public bool HasParameters
		{
			get
			{
				return _parameters != null && _parameters.Count > 0;
			}
		}

		public IDictionary<string, object> Parameters
		{
			get
			{
				if(_parameters == null)
					System.Threading.Interlocked.CompareExchange(ref _parameters, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase), null);

				return _parameters;
			}
		}
		#endregion
	}
}
