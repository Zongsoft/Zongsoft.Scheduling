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
		private int _requireScan = 0;
		private int _version = 0;
		private long _nextTick = 0;

		private DateTime? _nextTimestamp;
		private ConcurrentDictionary<ITrigger, ISet<IHandler>> _schedules;
		private ConcurrentDictionary<string, IHandler> _handlers;
		private ConcurrentQueue<ScheduleToken> _queue;
		private IDictionary<string, object> _states;
		#endregion

		#region 构造函数
		public Scheduler()
		{
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

			var isScan = false;

			if(_schedules.IsEmpty)
			{
				var original = Interlocked.CompareExchange(ref _requireScan, 1, 0);

				if(original == 0)
					isScan = true;
			}

			//获取指定触发器关联的执行处理器集合
			var handlers = _schedules.GetOrAdd(trigger, key => new HashSet<IHandler>());

			//将指定的执行处理器加入到对应的触发器的执行集合中
			if(handlers.Add(handler) && !isScan)
			{
				var next = trigger.GetNextOccurrence();

				if(next.HasValue && next.Value.Ticks <= _nextTick)
				{
					var original = Interlocked.Exchange(ref _nextTick, next.Value.Ticks);
					var version = Interlocked.Increment(ref _version);

					this.Fire(next.Value - DateTime.Now, new ScheduleToken[] { new ScheduleToken(trigger, handlers) }, version);
				}
			}

			if(isScan)
				this.Scan();
		}

		public void Schedule(ITrigger trigger, IHandler handler, Action<IHandlerContext> onTrigger)
		{
			throw new NotImplementedException();
		}

		public void Reschedule(IHandler handler, ITrigger trigger)
		{
			throw new NotImplementedException();
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
			Interlocked.Increment(ref _version);
		}

		protected override void OnResume()
		{
			this.Scan();
		}

		#region 私有方法
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
		private bool SetNext(DateTime timestamp, ScheduleToken token)
		{
			var nextTick = _nextTick;

			if(timestamp.Ticks < nextTick)
				Interlocked.CompareExchange(ref _nextTick, timestamp.Ticks, nextTick);

			if(_queue != null)
				_queue.Enqueue(token);

			return timestamp.Ticks <= nextTick;
		}

		private void Scan()
		{
			if(_schedules.IsEmpty)
				return;

			DateTime? next = null;
			var schedules = new List<ScheduleToken>();

			foreach(var schedule in _schedules)
			{
				var timestamp = schedule.Key.GetNextOccurrence(DateTime.Now);

				if(timestamp.HasValue && timestamp.Value <= next)
				{
					if(timestamp.Value < next)
						schedules.Clear();

					next = timestamp.Value;
					schedules.Add(new ScheduleToken(schedule));
				}
			}

			_nextTimestamp = next;

			if(next.HasValue)
			{
				_nextTick = next.Value.Ticks;
				this.Fire(next.Value - DateTime.Now, schedules, _version);
			}
		}

		private void Fire(TimeSpan delay, IEnumerable<ScheduleToken> schedules, int version)
		{
			if(schedules == null)
				return;

			Task.Delay(delay).ContinueWith((task, state) =>
			{
				var token = (TriggerToken)state;
				var original = Interlocked.CompareExchange(ref _version, 0, token.Version);

				if(original != token.Version)
					return;

				this.Scan();

				foreach(var schedule in token.Schedules)
				{
					foreach(var handler in schedule.Handlers)
					{
						handler.OnHandle(new HandlerContext(this, schedule.Trigger));
					}
				}
			}, new TriggerToken(version, schedules));
		}
		#endregion

		#region 嵌套子类
		private struct TriggerToken
		{
			public int Version;
			public IEnumerable<ScheduleToken> Schedules;

			public TriggerToken(int version, IEnumerable<ScheduleToken> schedules)
			{
				this.Version = version;
				this.Schedules = schedules;
			}
		}

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
