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

using Zongsoft.Services;

namespace Zongsoft.Scheduling.Commands
{
	public class SchedulerCommand : CommandBase<CommandContext>
	{
		#region 成员字段
		private IScheduler _scheduler;
		#endregion

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
				return _scheduler;
			}
			set
			{
				_scheduler = value ?? throw new ArgumentNullException();
			}
		}
		#endregion

		#region 执行方法
		protected override object OnExecute(CommandContext context)
		{
			if(context.Parameter is IScheduler scheduler)
				_scheduler = scheduler;

			return _scheduler;
		}
		#endregion

		#region 静态方法
		public static IScheduler GetScheduler(CommandTreeNode node)
		{
			if(node == null)
				return null;

			if(node.Command is SchedulerCommand command)
				return command.Scheduler;

			return GetScheduler(node.Parent);
		}
		#endregion
	}
}
