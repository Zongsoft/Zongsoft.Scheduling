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
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Zongsoft.Services;

namespace Zongsoft.Scheduling
{
	public class Scheduler : WorkerBase, IScheduler
	{
		#region 事件定义
		public event EventHandler<HandledEventArgs> Handled;
		public event EventHandler<OccurredEventArgs> Occurred;
		public event EventHandler<ScheduledEventArgs> Scheduled;
		#endregion

		#region 成员字段
		private long _nextTick = 0;
		private long _lastTick = 0;
		private CancellationTokenSource _cancellation;
		private ConcurrentDictionary<ITrigger, ScheduleToken> _schedules;
		private HashSet<IHandler> _handlers;
		private IDictionary<string, object> _states;
		#endregion

		#region 构造函数
		public Scheduler()
		{
			this.CanPauseAndContinue = true;

			_handlers = new HashSet<IHandler>();
			_schedules = new ConcurrentDictionary<ITrigger, ScheduleToken>(TriggerComparer.Instance);
		}
		#endregion

		#region 公共属性
		public DateTime? NextTime
		{
			get
			{
				var next = _nextTick;

				if(next == 0)
					return null;

				return new DateTime(next);
			}
		}

		public DateTime? LastTime
		{
			get
			{
				var recently = _lastTick;

				if(recently == 0)
					return null;

				return new DateTime(recently);
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
				foreach(var handler in _handlers)
					yield return handler;
			}
		}

		public bool HasStates
		{
			get
			{
				return _states != null && _states.Count > 0;
			}
		}

		public IDictionary<string, object> States
		{
			get
			{
				if(_states == null)
					Interlocked.CompareExchange(ref _states, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase), null);

				return _states;
			}
		}
		#endregion

		#region 公共方法
		public IEnumerable<IHandler> GetHandlers(ITrigger trigger)
		{
			if(trigger == null)
				throw new ArgumentNullException(nameof(trigger));

			if(_schedules.TryGetValue(trigger, out var schedule))
				return schedule.Handlers;
			else
				return System.Linq.Enumerable.Empty<IHandler>();
		}

		public bool Schedule(IHandler handler, ITrigger trigger)
		{
			if(trigger == null)
				throw new ArgumentNullException(nameof(trigger));
			if(handler == null)
				throw new ArgumentNullException(nameof(handler));

			if(_handlers.Add(handler))
				return this.ScheduleCore(handler, trigger);

			return false;
		}

		public bool Schedule(IHandler handler, ITrigger trigger, Action<IHandlerContext> onTrigger)
		{
			throw new NotImplementedException();
		}

		public void Reschedule(IHandler handler, ITrigger trigger)
		{
			if(handler == null)
				throw new ArgumentNullException(nameof(handler));
			if(trigger == null)
				throw new ArgumentNullException(nameof(trigger));

			if(_handlers.Add(handler))
			{
				this.ScheduleCore(handler, trigger);
			}
			else
			{
				foreach(var schedule in _schedules.Values)
				{
					if(schedule.Trigger.Equals(trigger))
					{
						schedule.AddHandler(handler);
						this.Refire(schedule);
					}
					else
					{
						schedule.RemoveHandler(handler);
					}
				}
			}
		}

		public void Unschedule()
		{
			var cancellation = _cancellation;

			if(cancellation != null)
				cancellation.Cancel();

			_handlers.Clear();
			_schedules.Clear();
		}

		public bool Unschedule(IHandler handler)
		{
			if(handler == null)
				return false;

			if(_handlers.Remove(handler))
			{
				foreach(var schedule in _schedules.Values)
				{
					schedule.RemoveHandler(handler);
				}

				return true;
			}

			return false;
		}

		public bool Unschedule(ITrigger trigger)
		{
			if(trigger == null)
				return false;

			if(_schedules.TryRemove(trigger, out var schedule))
			{
				schedule.ClearHandlers();
				return true;
			}

			return false;
		}
		#endregion

		#region 重写方法
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
		#endregion

		#region 激发事件
		protected virtual void OnHandled(IHandler handler, IHandlerContext context)
		{
			this.Handled?.Invoke(this, new HandledEventArgs(handler, context));
		}

		protected virtual void OnOccurred(int count)
		{
			this.Occurred?.Invoke(this, new OccurredEventArgs(count));
		}

		protected virtual void OnScheduled(int count, ITrigger[] triggers)
		{
			this.Scheduled?.Invoke(this, new ScheduledEventArgs(count, triggers));
		}
		#endregion

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

		private void Refire(ScheduleToken schedule)
		{
			if(this.State != WorkerState.Running)
				return;

			var next = schedule.Trigger.GetNextOccurrence();

			if(next.HasValue && (_nextTick == 0 || next.Value.Ticks <= _nextTick))
			{
				if(this.SetNext(next.Value))
					this.Fire(next.Value - Utility.Now(), new[] { schedule });
			}
		}

		private void Scan()
		{
			if(_schedules.IsEmpty)
				return;

			DateTime? next = null;
			var schedules = new List<ScheduleToken>();

			foreach(var schedule in _schedules.Values)
			{
				var timestamp = schedule.Trigger.GetNextOccurrence();

				if(timestamp.HasValue && (next == null || timestamp.Value <= next))
				{
					if(timestamp.Value < next)
						schedules.Clear();

					next = timestamp.Value;
					schedules.Add(schedule);
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
			if(schedules == null || delay < TimeSpan.Zero)
				return;

			var original = Interlocked.Exchange(ref _cancellation, new CancellationTokenSource());

			if(original != null)
				original.Cancel();

			Task.Delay(delay).ContinueWith((task, state) =>
			{
				//将上次激发时间点设为此时此刻
				_lastTick = _nextTick;

				this.Scan();

				//设置处理次数
				int count = 0;

				foreach(var schedule in (IEnumerable<ScheduleToken>)state)
				{
					foreach(var handler in schedule.Handlers)
					{
						//创建处理上下文
						var context = new HandlerContext(count++, this, schedule.Trigger);

						//调用处理器进行处理
						if(this.Handle(handler, context))
						{
							//激发“Handled”事件
							this.OnHandled(handler, context);
						}
					}
				}

				//激发“Occurred”事件
				this.OnOccurred(count);
			}, schedules, _cancellation.Token);

			try
			{
				//激发“Scheduled”事件
				this.OnScheduled(schedules.Sum(p => p.Count), schedules.Select(p => p.Trigger).ToArray());
			}
			catch(Exception ex)
			{
				Zongsoft.Diagnostics.Logger.Error(ex);
			}
		}

		private bool Handle(IHandler handler, IHandlerContext context)
		{
			try
			{
				//调用处理器进行处理
				handler.Handle(context);

				//返回调用成功
				return true;
			}
			catch(Exception ex)
			{
				//打印异常日志
				Zongsoft.Diagnostics.Logger.Error(ex);

				//返回调用失败
				return false;
			}
		}

		private bool ScheduleCore(IHandler handler, ITrigger trigger)
		{
			//获取指定触发器关联的执行处理器集合
			var schedule = _schedules.GetOrAdd(trigger, key => new ScheduleToken(key, new HashSet<IHandler>()));

			//将指定的执行处理器加入到对应的触发器的执行集合中，如果加入成功则尝试重新激发
			if(schedule.AddHandler(handler))
			{
				this.Refire(schedule);
				return true;
			}

			return false;
		}
		#endregion

		#region 嵌套子类
		private struct ScheduleToken
		{
			#region 公共字段
			public ITrigger Trigger;
			#endregion

			#region 私有变量
			private ISet<IHandler> _handlers;
			private AutoResetEvent _semaphore;
			#endregion

			#region 构造函数
			public ScheduleToken(ITrigger trigger, ISet<IHandler> handlers)
			{
				this.Trigger = trigger;
				this._handlers = handlers;
				_semaphore = new AutoResetEvent(true);
			}
			#endregion

			#region 公共属性
			public int Count
			{
				get
				{
					return _handlers.Count;
				}
			}

			public IEnumerable<IHandler> Handlers
			{
				get
				{
					try
					{
						_semaphore.WaitOne();

						foreach(var handler in _handlers)
						{
							yield return handler;
						}
					}
					finally
					{
						_semaphore.Set();
					}
				}
			}
			#endregion

			#region 公共方法
			public bool AddHandler(IHandler handler)
			{
				if(handler == null)
					return false;

				try
				{
					_semaphore.WaitOne();

					var handlers = this._handlers as ISet<IHandler>;

					if(handlers != null)
						return handlers.Add(handler);

					return false;
				}
				finally
				{
					_semaphore.Set();
				}
			}

			public bool RemoveHandler(IHandler handler)
			{
				if(handler == null)
					return false;

				try
				{
					_semaphore.WaitOne();

					var handlers = this._handlers as ISet<IHandler>;

					if(handlers != null)
						return handlers.Remove(handler);

					return false;
				}
				finally
				{
					_semaphore.Set();
				}
			}

			public void ClearHandlers()
			{
				try
				{
					_semaphore.WaitOne();
					this._handlers.Clear();
				}
				finally
				{
					_semaphore.Set();
				}
			}
			#endregion
		}

		private class TriggerComparer : IEqualityComparer<ITrigger>
		{
			#region 单例字段
			public static readonly TriggerComparer Instance = new TriggerComparer();
			#endregion

			#region 私有构造
			private TriggerComparer()
			{
			}
			#endregion

			#region 公共方法
			public bool Equals(ITrigger x, ITrigger y)
			{
				if(x == null || y == null)
					return false;

				return x.GetType() == y.GetType() && x.Equals(y);
			}

			public int GetHashCode(ITrigger obj)
			{
				if(obj == null)
					return 0;

				return obj.GetType().GetHashCode() ^ obj.GetHashCode();
			}
			#endregion
		}
		#endregion
	}
}
