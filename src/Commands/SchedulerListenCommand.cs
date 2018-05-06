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
using System.Threading;

using Zongsoft.Common;
using Zongsoft.Services;

namespace Zongsoft.Scheduling.Commands
{
	public class SchedulerListenCommand : Zongsoft.Services.Commands.WorkerListenCommand
	{
		#region 构造函数
		public SchedulerListenCommand()
		{
		}

		public SchedulerListenCommand(string name) : base(name)
		{
		}
		#endregion

		#region 重写方法
		protected override void OnListening(CommandContext context, IWorker worker)
		{
			var scheduler = worker as IScheduler;

			if(scheduler == null)
				throw new CommandException("");

			scheduler.Handled += this.Scheduler_Handled;
			scheduler.Occurred += this.Scheduler_Occurred;
			scheduler.Scheduled += this.Scheduler_Scheduled;

			if(scheduler.Retriever != null)
			{
				scheduler.Retriever.Failed += this.Retriever_Failed;
				scheduler.Retriever.Succeed += this.Retriever_Succeed;
			}

			//调用基类同名方法（打印欢迎信息）
			base.OnListening(context, worker);

			//打印基本信息
			context.Output.WriteLine(SchedulerCommand.GetInfo(scheduler, true).AppendLine());
		}

		protected override void OnListened(CommandContext context, IWorker worker)
		{
			var scheduler = worker as IScheduler;

			if(scheduler == null)
				return;

			scheduler.Handled -= this.Scheduler_Handled;
			scheduler.Occurred -= this.Scheduler_Occurred;
			scheduler.Scheduled -= this.Scheduler_Scheduled;

			if(scheduler.Retriever != null)
			{
				scheduler.Retriever.Failed -= this.Retriever_Failed;
				scheduler.Retriever.Succeed -= this.Retriever_Succeed;
			}
		}
		#endregion

		#region 事件处理
		private void Scheduler_Handled(object sender, HandledEventArgs e)
		{
			//根据处理完成事件参数来设置标志名
			var name = e.Exception == null ? Properties.Resources.Scheduler_Handled_Succeed : Properties.Resources.Scheduler_Handled_Failed;

			//获取处理完成的事件信息内容
			var content = this.GetHandledContent(name, e);

			//输出事件信息内容
			this.Context.Output.WriteLine(content);
		}

		private void Scheduler_Occurred(object sender, OccurredEventArgs e)
		{
			//获取调度器的基本信息内容（不需包含状态信息）
			var content = SchedulerCommand.GetInfo((IScheduler)sender, false);

			content.Prepend(Properties.Resources.Scheduler_Occurred_Name)
				.After(CommandOutletColor.DarkGray, "(")
				.After(CommandOutletColor.DarkCyan, e.ScheduleId)
				.After(CommandOutletColor.DarkGray, "): ")
				.After(CommandOutletColor.Magenta, e.Count.ToString() + " ");

			this.Context.Output.WriteLine(content.First);
		}

		private void Scheduler_Scheduled(object sender, ScheduledEventArgs e)
		{
			//获取调度器的基本信息内容（不需包含状态信息）
			var content = SchedulerCommand.GetInfo((IScheduler)sender, false);

			content.Prepend(Properties.Resources.Scheduler_Scheduled_Name)
				.After(CommandOutletColor.DarkGray, "(")
				.After(CommandOutletColor.DarkCyan, e.ScheduleId)
				.After(CommandOutletColor.DarkGray, "): ")
				.After(CommandOutletColor.Magenta, e.Count.ToString() + " ");

			if(e.Triggers != null && e.Triggers.Length > 0)
			{
				content.AppendLine();

				for(int i = 0; i < e.Triggers.Length; i++)
				{
					content.Append(CommandOutletColor.DarkYellow, $"[{i + 1}] ")
					       .AppendLine(e.Triggers[i].ToString());
				}
			}

			this.Context.Output.WriteLine(content.First);
		}

		private void Retriever_Failed(object sender, HandledEventArgs e)
		{
			//获取重试失败的事件信息内容
			var content = this.GetHandledContent(Properties.Resources.Retriever_Failed_Name, e);

			//输出事件信息内容
			this.Context.Output.WriteLine(content);
		}

		private void Retriever_Succeed(object sender, HandledEventArgs e)
		{
			//获取重试成功的事件信息内容
			var content = this.GetHandledContent(Properties.Resources.Retriever_Succeed_Name, e);

			//输出事件信息内容
			this.Context.Output.WriteLine(content);
		}
		#endregion

		#region 私有方法
		private CommandOutletContent GetHandledContent(string name, HandledEventArgs args)
		{
			var content = CommandOutletContent.Create(name)
				.Append(CommandOutletColor.DarkGray, "(")
				.Append(CommandOutletColor.DarkCyan, args.Context.ScheduleId.ToString())
				.Append(CommandOutletColor.DarkGray, "): ")
				.Append(CommandOutletColor.DarkYellow, $"[{args.Context.Index + 1}] ")
				.Append(CommandOutletColor.DarkCyan, args.Handler.ToString())
				.Append(CommandOutletColor.DarkGray, "@")
				.Append(CommandOutletColor.DarkMagenta, args.Context.Trigger.ToString());

			if(args.Context.Failure.HasValue)
			{
				var failure = args.Context.Failure.Value;

				//为重试信息添加起始标记
				content.Append(CommandOutletColor.Gray, " {");

				if(failure.Count > 0 && failure.Timestamp.HasValue)
				{
					content.Append(CommandOutletColor.DarkYellow, "#");
					content.Append(CommandOutletColor.DarkRed, failure.Count.ToString());
					content.Append(CommandOutletColor.DarkYellow, "#");

					content.Append(CommandOutletColor.DarkGray, " (");
					content.Append(CommandOutletColor.DarkRed, failure.Timestamp.HasValue ? failure.Timestamp.ToString() : Properties.Resources.Scheduler_Retry_NoTimestamp);
					content.Append(CommandOutletColor.DarkGray, ")");
				}

				if(failure.Expiration.HasValue)
				{
					content.Append(CommandOutletColor.Red, " < ");
					content.Append(CommandOutletColor.DarkGray, "(");
					content.Append(CommandOutletColor.DarkMagenta, failure.Expiration.HasValue ? failure.Expiration.ToString() : Properties.Resources.Scheduler_Retry_NoExpiration);
					content.Append(CommandOutletColor.DarkGray, ")");
				}

				//为重试信息添加结束标记
				content.Append(CommandOutletColor.Gray, "}");
			}

			if(args.Exception != null)
			{
				//设置名称内容端的文本颜色为红色
				content.First.Color = CommandOutletColor.Red;

				content.AppendLine()
				       .Append(this.GetExceptionContent(args.Exception));
			}

			return content;
		}

		private CommandOutletContent GetExceptionContent(Exception exception)
		{
			if(exception == null)
				return null;

			//哔哔一下
			Console.Beep();

			//为异常信息添加起始标记
			var content = CommandOutletContent.Create(CommandOutletColor.Gray, "{" + Environment.NewLine);

			while(exception != null)
			{
				content.Append(CommandOutletColor.Red, "    " + exception.GetType().FullName);

				if(!string.IsNullOrEmpty(exception.Source))
				{
					content.Append(CommandOutletColor.DarkGray, "(")
					       .Append(CommandOutletColor.DarkYellow, exception.Source)
					       .Append(CommandOutletColor.DarkGray, ")");
				}

				content.Append(CommandOutletColor.DarkGray, ": ")
				       .AppendLine(exception.Message.Replace('\r', ' ').Replace('\n', ' '));

				exception = exception.InnerException;
			}

			//为异常信息添加结束标记
			return content.Append(CommandOutletColor.Gray, "}");
		}
		#endregion
	}
}
