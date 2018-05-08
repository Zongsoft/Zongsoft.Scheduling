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
	/// <summary>
	/// 表示调度处理上下文的接口。
	/// </summary>
	public interface IHandlerContext
	{
		/// <summary>
		/// 获取调度事务中的处理序号。
		/// </summary>
		int Index
		{
			get;
		}

		/// <summary>
		/// 获取调度任务编号。
		/// </summary>
		string ScheduleId
		{
			get;
		}

		/// <summary>
		/// 获取任务首次调度的时间。
		/// </summary>
		DateTime Timestamp
		{
			get;
		}

		/// <summary>
		/// 获取处理失败的扩展信息。
		/// </summary>
		HandlerFailure? Failure
		{
			get;
			set;
		}

		/// <summary>
		/// 获取处理的调度器对象。
		/// </summary>
		IScheduler Scheduler
		{
			get;
		}

		/// <summary>
		/// 获取关联的触发器对象。
		/// </summary>
		ITrigger Trigger
		{
			get;
		}

		/// <summary>
		/// 获取一个值，指示上下文是否含有扩展参数。
		/// </summary>
		bool HasParameters
		{
			get;
		}

		/// <summary>
		/// 获取上下文的扩展参数集。
		/// </summary>
		IDictionary<string, object> Parameters
		{
			get;
		}
	}
}
