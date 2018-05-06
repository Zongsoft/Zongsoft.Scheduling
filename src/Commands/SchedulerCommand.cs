﻿/*
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

using Zongsoft.Services;

namespace Zongsoft.Scheduling.Commands
{
	public class SchedulerCommand : Zongsoft.Services.Commands.WorkerCommandBase
	{
		#region 构造函数
		public SchedulerCommand() : base("Scheduler")
		{
		}

		public SchedulerCommand(string name) : base(name)
		{
		}
		#endregion

		#region 公共属性
		public IScheduler Scheduler
		{
			get
			{
				return this.Worker as IScheduler;
			}
			set
			{
				this.Worker = value;
			}
		}
		#endregion

		#region 静态方法
		/// <summary>
		/// 获取指定调度器的基本信息内容。
		/// </summary>
		/// <param name="scheduler">指定要获取的调度器对象。</param>
		/// <param name="includeState">指定是否要包含调度器的状态信息。</param>
		/// <returns>返回生成的调度器基本信息的内容。</returns>
		public static CommandOutletContent GetInfo(IScheduler scheduler, bool includeState)
		{
			var content = CommandOutletContent.Create(CommandOutletColor.DarkCyan, scheduler.Name + " ")
				.Append(CommandOutletColor.DarkGray, "(")
				.Append(CommandOutletColor.Cyan, $"{scheduler.Triggers.Count}, {scheduler.Handlers.Count}")
				.Append(CommandOutletColor.DarkGray, ") ")
				.Append(CommandOutletColor.DarkYellow,      //最近执行时间
					scheduler.LastTime.HasValue ? "(" + scheduler.LastTime.Value.ToString() + ")" : Properties.Resources.Scheduler_NoLastTime)
				.Append(CommandOutletColor.DarkCyan, " ~ ")
				.Append(CommandOutletColor.DarkYellow,  //下次执行时间
					scheduler.NextTime.HasValue ? "(" + scheduler.NextTime.Value.ToString() + ")" : Properties.Resources.Scheduler_NoNextTime);

			if(includeState)
				return content.Prepend(GetStateColor(scheduler), $"[{scheduler.State}] ");

			return content;
		}
		#endregion
	}
}
