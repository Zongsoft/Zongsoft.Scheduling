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
	/// 表示调度器的接口。
	/// </summary>
	public interface IScheduler : Zongsoft.Services.IWorker
	{
		#region 事件声明
		/// <summary>表示一个处理器执行完成的事件。</summary>
		/// <remarks>通过<seealso cref="IHandlerContext.Failure"/>属性来确认当前处理完成是否为重试完成以及重试情况的信息。</remarks>
		event EventHandler<HandledEventArgs> Handled;

		/// <summary>表示一次处理执行完成的事件，该事件总是处于<seealso cref="Handled"/>事件之后。</summary>
		/// <remarks>即使一次处理执行中的所有处理器都调用失败，该事件也会发生。</remarks>
		event EventHandler<OccurredEventArgs> Occurred;

		/// <summary>表示一个处理器调度完成的事件。</summary>
		event EventHandler<ScheduledEventArgs> Scheduled;
		#endregion

		#region 属性声明
		/// <summary>
		/// 获取一个值，指示最近一次调度的时间。
		/// </summary>
		DateTime? LastTime
		{
			get;
		}

		/// <summary>
		/// 获取一个值，指示下一次调度的时间。
		/// </summary>
		DateTime? NextTime
		{
			get;
		}

		/// <summary>
		/// 获取或设置调度失败的重试器。
		/// </summary>
		IRetriever Retriever
		{
			get;
			set;
		}

		/// <summary>
		/// 获取调度器中的调度触发器集。
		/// </summary>
		IReadOnlyCollection<ITrigger> Triggers
		{
			get;
		}

		/// <summary>
		/// 获取调度器中的调度处理器集。
		/// </summary>
		IReadOnlyCollection<IHandler> Handlers
		{
			get;
		}

		/// <summary>
		/// 获取一个值，指示当前调度器是否处于工作中。
		/// </summary>
		bool IsScheduling
		{
			get;
		}

		/// <summary>
		/// 获取一个值，指示当前调度器是否含有附加数据。
		/// </summary>
		bool HasStates
		{
			get;
		}

		/// <summary>
		/// 获取当前调度器的附加数据字典。
		/// </summary>
		IDictionary<string, object> States
		{
			get;
		}
		#endregion

		#region 方法声明
		/// <summary>
		/// 获取指定触发器中关联的处理器。
		/// </summary>
		/// <param name="trigger">指定要获取的触发器。</param>
		/// <returns>返回指定触发器中关联的处理器集。</returns>
		IEnumerable<IHandler> GetHandlers(ITrigger trigger);

		bool Schedule(IHandler handler, ITrigger trigger);
		void Reschedule(IHandler handler, ITrigger trigger);

		void Unschedule();
		bool Unschedule(IHandler handler);
		bool Unschedule(ITrigger trigger);
		#endregion
	}
}
