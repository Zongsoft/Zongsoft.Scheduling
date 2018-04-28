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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Zongsoft.Services;
using Zongsoft.Collections;

namespace Zongsoft.Scheduling
{
	public class Scheduler : WorkerBase, IScheduler
	{
		#region 成员字段
		private long _nextTick = 0;
		private CancellationTokenSource _cancellation;

		private ConcurrentDictionary<ITrigger, ISet<IHandler>> _schedules;
		private ConcurrentDictionary<string, IHandler> _handlers;
		private IDictionary<string, object> _states;
		#endregion

		#region 构造函数
		public Scheduler()
		{
			this.CanPauseAndContinue = true;

			_handlers = new ConcurrentDictionary<string, IHandler>();
			_schedules = new ConcurrentDictionary<ITrigger, ISet<IHandler>>();
			_states = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		}
		#endregion

		public DateTime? NextOccurrence
		{
			get
			{
				var next = _nextTick;

				if(next == 0)
					return null;

				return new DateTime(next);
			}
		}

		public IEnumerable<ITrigger> Triggers
		{
			get
			{
				foreach(var trigger in _schedules.Keys)
				{
					yield return trigger;
				}
			}
		}

		public IEnumerable<IHandler> Handlers
		{
			get
			{
				foreach(var handler in _handlers.Values)
					yield return handler;
			}
		}

		public IDictionary<string, object> States
		{
			get
			{
				return _states;
			}
		}

		public ITrigger GetTrigger(object parameter)
		{
			foreach(var trigger in _schedules.Keys)
			{
				if(trigger is IMatchable matchable && matchable.IsMatch(parameter))
					return trigger;
			}

			return null;
		}

		public IEnumerable<ITrigger> GetTriggers(object parameter)
		{
			foreach(var trigger in _schedules.Keys)
			{
				if(trigger is IMatchable matchable && matchable.IsMatch(parameter))
					yield return trigger;
			}
		}

		public IHandler GetHandler(string name)
		{
			if(name != null && _handlers.TryGetValue(name, out var handler))
				return handler;

			return null;
		}

		public IEnumerable<IHandler> GetHandlers(ITrigger trigger)
		{
			if(trigger == null)
				throw new ArgumentNullException(nameof(trigger));

			if(_schedules.TryGetValue(trigger, out var handlers))
			{
				foreach(var handler in handlers)
					yield return handler;
			}
		}

		public void Schedule(ITrigger trigger, IHandler handler)
		{
			if(trigger == null)
				throw new ArgumentNullException(nameof(trigger));
			if(handler == null)
				throw new ArgumentNullException(nameof(handler));

			//获取指定触发器关联的执行处理器集合
			var handlers = _schedules.GetOrAdd(trigger, key => new HashSet<IHandler>());

			//将指定的执行处理器加入到对应的触发器的执行集合中
			//如果加入成功并且当前状态为运行中则有可能需要手动激发，
			//如果当前状态不是运行中，则无需手动激发，可由OnStart或OnResume来触发
			if(handlers.Add(handler) && this.State == WorkerState.Running)
			{
				var next = trigger.GetNextOccurrence();

				if(next.HasValue && (_nextTick == 0 || next.Value.Ticks <= _nextTick))
				{
					if(this.SetNext(next.Value))
						this.Fire(next.Value - Utility.Now(), new ScheduleToken[] { new ScheduleToken(trigger, handlers) });
				}
			}
		}

		public void Schedule(ITrigger trigger, IHandler handler, Action<IHandlerContext> onTrigger)
		{
			throw new NotImplementedException();
		}

		public void Reschedule(IHandler handler, ITrigger trigger)
		{
			throw new NotImplementedException();
		}

		public void Unschedule()
		{
			_schedules.Clear();
			_handlers.Clear();
		}

		public bool Unschedule(IHandler handler)
		{
			if(handler == null)
				return false;

			if(_handlers.TryRemove(handler.Name, out var _))
			{
				foreach(var handlers in _schedules.Values)
				{
					handlers.Remove(handler);
				}

				return true;
			}

			return false;
		}

		public bool Unschedule(ITrigger trigger)
		{
			if(trigger == null)
				return false;

			if(_schedules.TryRemove(trigger, out var handlers))
			{
				handlers.Clear();
				return true;
			}

			return false;
		}

		protected override void OnStart(string[] args)
		{
			this.Scan();
		}

		protected override void OnStop(string[] args)
		{
			var cancellation = _cancellation;

			if(cancellation != null)
				cancellation.Cancel();
		}

		protected override void OnPause()
		{
			var cancellation = _cancellation;

			if(cancellation != null)
				cancellation.Cancel();
		}

		protected override void OnResume()
		{
			this.Scan();
		}

		#region 私有方法
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
		private bool SetNext(DateTime timestamp)
		{
			var nextTick = _nextTick;

			if(nextTick == 0 || timestamp.Ticks < nextTick)
			{
				_nextTick = timestamp.Ticks;
				return true;
			}

			return false;
		}

		private void Scan()
		{
			if(_schedules.IsEmpty)
				return;

			DateTime? next = null;
			var schedules = new List<ScheduleToken>();

			foreach(var schedule in _schedules)
			{
				var timestamp = schedule.Key.GetNextOccurrence();

				if(timestamp.HasValue && (next == null || timestamp.Value <= next))
				{
					if(timestamp.Value < next)
						schedules.Clear();

					next = timestamp.Value;
					schedules.Add(new ScheduleToken(schedule));
				}
			}

			if(next.HasValue)
			{
				_nextTick = next.Value.Ticks;
				this.Fire(next.Value - Utility.Now(), schedules);
			}
		}

		private void Fire(TimeSpan delay, IEnumerable<ScheduleToken> schedules)
		{
			if(schedules == null)
				return;

			var original = Interlocked.Exchange(ref _cancellation, new CancellationTokenSource());

			if(original != null)
			{
				original.Cancel();
				original.Dispose();
			}

			Task.Delay(delay).ContinueWith((task, state) =>
			{
				this.Scan();

				foreach(var schedule in (IEnumerable<ScheduleToken>)state)
				{
					foreach(var handler in schedule.Handlers)
					{
						try
						{
							handler.OnHandle(new HandlerContext(this, schedule.Trigger));
						}
						catch { }
					}
				}
			}, schedules, _cancellation.Token);
		}
		#endregion

		#region 嵌套子类
		private struct ScheduleToken
		{
			public ITrigger Trigger;
			public IEnumerable<IHandler> Handlers;

			public ScheduleToken(ITrigger trigger, IEnumerable<IHandler> handlers)
			{
				this.Trigger = trigger;
				this.Handlers = handlers;
			}

			public ScheduleToken(KeyValuePair<ITrigger, ISet<IHandler>> pair)
			{
				this.Trigger = pair.Key;
				this.Handlers = pair.Value;
			}
		}
		#endregion
	}
}
