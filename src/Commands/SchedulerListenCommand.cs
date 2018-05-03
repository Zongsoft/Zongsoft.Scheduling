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
using System.Threading;

using Zongsoft.Common;
using Zongsoft.Services;
using Zongsoft.Terminals;

namespace Zongsoft.Scheduling.Commands
{
	public class SchedulerListenCommand : CommandBase<CommandContext>
	{
		#region 私有变量
		private ICommandOutlet _outlet;
		private IScheduler _scheduler;
		private AutoResetEvent _semaphore;
		#endregion

		#region 构造函数
		public SchedulerListenCommand() : base("Listen")
		{
			_semaphore = new AutoResetEvent(false);
		}

		public SchedulerListenCommand(string name) : base(name)
		{
			_semaphore = new AutoResetEvent(false);
		}
		#endregion

		#region 重写方法
		protected override object OnExecute(CommandContext context)
		{
			//获取当前命令执行器对应的终端
			var terminal = (context.Executor as TerminalCommandExecutor)?.Terminal;

			//如果当前命令执行器不是终端命令执行器则抛出不支持的异常
			if(terminal == null)
				throw new NotSupportedException("The listen command must be run in terminal executor.");

			//查找获取当前命令对应的调度器对象
			_scheduler = SchedulerCommand.GetScheduler(context.CommandNode);

			//如果调度器查找失败，则抛出异常
			if(_scheduler == null)
				throw new InvalidOperationException($"The required scheduler not found.");

			//保存当前命令上下文的输出端子
			_outlet = context.Output;

			//挂载当前终端的中断事件
			terminal.Aborting += this.Terminal_Aborting;

			//绑定调度器的各种侦听事件
			this.Bind(_scheduler);

			//打印欢迎信息
			this.PrintWelcome(_scheduler);
			//打印基本信息
			this.PrintInfo(_scheduler);

			//等待信号量
			_semaphore.WaitOne();

			//取消所有侦听事件
			this.Unbind(_scheduler);

			//返回侦听的调度器
			return _scheduler;
		}
		#endregion

		#region 事件处理
		private void Scheduler_StateChanged(object sender, WorkerStateChangedEventArgs e)
		{
			this.PrintInfo((IScheduler)sender);
		}

		private void Scheduler_Handled(object sender, HandledEventArgs e)
		{
			var extra = string.Empty;

			if(e.Context.Failure.HasValue)
			{
				if(e.Context.Failure.Value.Count == 0 || e.Context.Failure.Value.Timestamp == null)
					extra = Properties.Resources.Scheduler_Retry_First;
				else
					extra = string.Format(Properties.Resources.Scheduler_Retry_Counting, e.Context.Failure.Value.Count, e.Context.Failure.Value.Expiration.Value);

				if(e.Context.Failure.Value.Expiration.HasValue)
					extra += " " + string.Format(Properties.Resources.Scheduler_Retry_Expiration, e.Context.Failure.Value.Expiration.Value);
				else
					extra += " " + Properties.Resources.Scheduler_Retry_Unlimited;
			}

			_outlet.WriteLine(CommandOutletColor.Green,
			                  string.Format(Properties.Resources.Scheduler_Handled, e.Context.Index, e.Handler, e.Context.Trigger, extra));
		}

		private void Scheduler_Occurred(object sender, OccurredEventArgs e)
		{
			_outlet.WriteLine(CommandOutletColor.Magenta, Properties.Resources.Scheduler_Occurred, e.Count);
			_outlet.WriteLine(this.GetMessage(sender as IScheduler));
		}

		private void Scheduler_Scheduled(object sender, ScheduledEventArgs e)
		{
			var message = string.Format(Properties.Resources.Scheduler_Scheduled, e.Count) + Environment.NewLine;

			for(int i = 0; i < e.Triggers.Length; i++)
			{
				message += $"[{i + 1}] {e.Triggers[i]}" + Environment.NewLine;
			}

			_outlet.WriteLine(CommandOutletColor.DarkGreen, message);
			_outlet.WriteLine(this.GetMessage(sender as IScheduler));
		}

		private void Retriever_Failed(object sender, HandledEventArgs e)
		{
			Console.Beep();

			var extra = string.Empty;

			if(e.Context.Failure.Value.Expiration == null)
				extra = Properties.Resources.Scheduler_Retry_Unlimited;
			else
				extra = string.Format(Properties.Resources.Scheduler_Retry_Expiration, e.Context.Failure.Value.Expiration.Value);

			var message = string.Format(Properties.Resources.Scheduler_Retry_Failed,
				e.Handler,
				e.Trigger,
				e.Context.Failure.Value.Count,
				e.Context.Failure.Value.Timestamp);

			_outlet.WriteLine(CommandOutletColor.Red, $"{message}\t {extra}");
		}

		private void Retriever_Succeed(object sender, HandledEventArgs e)
		{
			//_outlet.Write(CommandOutletColor.Green, Properties.Resources.Scheduler_Retry_Succeed);
		}

		private void Terminal_Aborting(object sender, System.ComponentModel.CancelEventArgs e)
		{
			//阻止命令执行器被关闭
			e.Cancel = true;

			//释放信号量
			_semaphore.Set();
		}
		#endregion

		#region 私有方法
		private void PrintWelcome(IScheduler scheduler)
		{
			_outlet.WriteLine(Properties.Resources.Scheduler_Listen_Welcome, scheduler.Name);
			_outlet.WriteLine(CommandOutletColor.DarkYellow, Properties.Resources.Scheduler_Listen_ToExit_Prompt + Environment.NewLine);
		}

		private void PrintInfo(IScheduler scheduler)
		{
			var state = this.GetState(scheduler, out var color);
			var message = this.GetMessage(scheduler);

			_outlet.Write(color, state);
			_outlet.WriteLine(message + Environment.NewLine);
		}

		private string GetState(IScheduler scheduler, out CommandOutletColor color)
		{
			var state = scheduler.State;

			switch(state)
			{
				case WorkerState.Pausing:
				case WorkerState.Paused:
					color = CommandOutletColor.DarkYellow;
					break;
				case WorkerState.Resuming:
				case WorkerState.Starting:
					color = CommandOutletColor.DarkGreen;
					break;
				case WorkerState.Stopped:
				case WorkerState.Stopping:
					color = CommandOutletColor.Gray;
					break;
				default:
					color = CommandOutletColor.Green;
					break;
			}

			return $"[{state}] ";
		}

		private string GetMessage(IScheduler scheduler)
		{
			var lastTime = scheduler.LastTime.HasValue ? string.Format(Properties.Resources.Scheduler_LastTime, scheduler.LastTime.Value.ToString()) : Properties.Resources.Scheduler_NoLastTime;
			var nextTime = scheduler.NextTime.HasValue ? string.Format(Properties.Resources.Scheduler_NextTime, scheduler.NextTime.Value.ToString()) : Properties.Resources.Scheduler_NoNextTime;

			return string.Format(Properties.Resources.Scheduler_Counting, scheduler.Triggers.Count, scheduler.Handlers.Count) +
			       " | " + lastTime + " | " + nextTime + Environment.NewLine;
		}

		private void Bind(IScheduler scheduler)
		{
			if(scheduler == null)
				return;

			scheduler.Handled += this.Scheduler_Handled;
			scheduler.Occurred += this.Scheduler_Occurred;
			scheduler.Scheduled += this.Scheduler_Scheduled;
			scheduler.StateChanged += this.Scheduler_StateChanged;

			if(scheduler.Retriever != null)
			{
				scheduler.Retriever.Failed += this.Retriever_Failed;
				scheduler.Retriever.Succeed += this.Retriever_Succeed;
			}
		}

		private void Unbind(IScheduler scheduler)
		{
			if(scheduler == null)
				return;

			scheduler.Handled -= this.Scheduler_Handled;
			scheduler.Occurred -= this.Scheduler_Occurred;
			scheduler.Scheduled -= this.Scheduler_Scheduled;
			scheduler.StateChanged -= this.Scheduler_StateChanged;

			if(scheduler.Retriever != null)
			{
				scheduler.Retriever.Failed -= this.Retriever_Failed;
				scheduler.Retriever.Succeed -= this.Retriever_Succeed;
			}
		}
		#endregion
	}
}
