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
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Zongsoft.Services;
using System.Collections;

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
		private TriggerCollection _triggers;
		private IDictionary<string, object> _states;
		private IRetriever _retriever;
		#endregion

		#region 构造函数
		public Scheduler()
		{
			this.CanPauseAndContinue = true;

			_handlers = new HashSet<IHandler>();
			_schedules = new ConcurrentDictionary<ITrigger, ScheduleToken>(TriggerComparer.Instance);
			_triggers = new TriggerCollection(_schedules);

			_retriever = new Retriever();
			_retriever.Succeed += Retriever_Retried;
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

		public IRetriever Retriever
		{
			get
			{
				return _retriever;
			}
			[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
			set
			{
				if(value == null)
					throw new ArgumentNullException();

				if(object.ReferenceEquals(_retriever, value))
					return;

				//保存原有重试器
				var original = _retriever;

				_retriever.Succeed -= Retriever_Retried;
				_retriever = value;
				_retriever.Succeed += Retriever_Retried;

				//通知子类该属性值发生了改变
				this.OnRetrieverChanged(value, original);
			}
		}

		public IReadOnlyCollection<ITrigger> Triggers
		{
			get
			{
				return _triggers;
			}
		}

		public IReadOnlyCollection<IHandler> Handlers
		{
			get
			{
				return _handlers;
			}
		}

		public bool IsScheduling
		{
			get
			{
				return _cancellation != null && !_cancellation.IsCancellationRequested;
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
			return this.Schedule(handler, trigger, null);
		}

		public bool Schedule(IHandler handler, ITrigger trigger, Action<IHandlerContext> onTrigger)
		{
			if(trigger == null)
				throw new ArgumentNullException(nameof(trigger));
			if(handler == null)
				throw new ArgumentNullException(nameof(handler));

			if(onTrigger != null) //TODO: 暂时不支持该功能
				throw new NotSupportedException();

			//将处理器增加到处理器集中，如果添加成功（说明该处理器没有被调度过）
			if(_handlers.Add(handler))
			{
				//将该处理器加入到指定的触发器中的调度处理集
				return this.ScheduleCore(handler, trigger);
			}

			return false;
		}

		public void Reschedule(IHandler handler, ITrigger trigger)
		{
			if(handler == null)
				throw new ArgumentNullException(nameof(handler));
			if(trigger == null)
				throw new ArgumentNullException(nameof(trigger));

			//将处理器增加到处理器集中，如果添加成功（说明该处理器没有被调度过）
			if(_handlers.Add(handler))
			{
				//将该处理器加入到指定的触发器中的调度处理集
				this.ScheduleCore(handler, trigger);

				//执行完成
				return;
			}

			//定义找到的调度项变量（默认没有找到）
			ScheduleToken? found = null;

			//循环遍历排程集，查找重新排程的触发器
			foreach(var schedule in _schedules.Values)
			{
				//如果当前排程的触发器等于要重新排程的触发器，则更新找到引用
				if(schedule.Trigger.Equals(trigger))
				{
					found = schedule;
				}
				else //否则就尝试将待排程的处理器从原有排程项的处理集中移除掉
				{
					schedule.RemoveHandler(handler);
				}
			}

			if(found.HasValue)
			{
				//将指定的执行处理器加入到找到的调度项的执行集合中，如果加入成功则尝试重新激发
				//该新增方法确保同步完成，不会引发线程重入导致的状态不一致
				if(found.Value.AddHandler(handler))
					this.Refire(found.Value);
			}
			else
			{
				//将该处理器加入到指定的触发器中的调度处理集
				this.ScheduleCore(handler, trigger);
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
			//扫描调度集
			this.Scan();

			//启动失败重试队列
			_retriever.Run();
		}

		protected override void OnStop(string[] args)
		{
			var cancellation = _cancellation;

			if(cancellation != null)
				cancellation.Cancel();

			//清除下次触发时间
			_nextTick = 0;

			//停止失败重试队列并清空所有待重试项
			_retriever.Stop(true);
		}

		protected override void OnPause()
		{
			var cancellation = _cancellation;

			if(cancellation != null)
				cancellation.Cancel();

			//清除下次触发时间
			_nextTick = 0;

			//停止失败重试队列
			_retriever.Stop(false);
		}

		protected override void OnResume()
		{
			//扫描调度集
			this.Scan();

			//启动失败重试队列
			_retriever.Run();
		}
		#endregion

		#region 虚拟方法
		protected virtual void OnRetrieverChanged(IRetriever newRetriever, IRetriever oldRetriever)
		{
		}
		#endregion

		#region 扫描方法
		/// <summary>
		/// 重新扫描排程集，并规划最新的调度任务。
		/// </summary>
		/// <remarks>
		///		<para>对调用者的建议：该方法只应在异步启动中调用。</para>
		/// </remarks>
		protected void Scan()
		{
			//如果排程集为空则退出扫描
			if(_schedules.IsEmpty)
				return;

			DateTime? earliest = null;
			var schedules = new List<ScheduleToken>();

			//循环遍历排程集，找出其中最早的触发时间点
			foreach(var schedule in _schedules.Values)
			{
				//获取当前排程项的下次触发时间
				var timestamp = schedule.Trigger.GetNextOccurrence();

				if(timestamp.HasValue && (earliest == null || timestamp.Value <= earliest))
				{
					//如果下次触发时间比之前找到的最早项还早，则将之前的排程列表清空
					if(timestamp.Value < earliest)
						schedules.Clear();

					//更新当前最早触发时间点
					earliest = timestamp.Value;

					//将找到的最早排程项加入到列表中
					schedules.Add(schedule);
				}
			}

			//如果找到最早的触发时间，则将找到的排程项列表加入到调度进程中
			if(earliest.HasValue)
				this.Fire(earliest.Value, schedules);
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
		private void Refire(ScheduleToken schedule)
		{
			//如果当前任务取消标记为空（表示还没有启动排程）或任务取消标记已经被取消过（表示任务处于暂停或停止状态）
			if(_cancellation == null || _cancellation.IsCancellationRequested)
				return;

			//获取下次触发的时间点
			var timestamp = schedule.Trigger.GetNextOccurrence();

			//如果下次触发时间不为空（即需要触发）并且触发时间小于已经排程中的下次待触发时间，则立刻重新调度
			if(timestamp.HasValue && (_nextTick == 0 || timestamp.Value.Ticks < _nextTick))
			{
				//将当前排程项立刻插入到排程进度中，即替换当前待触发的排程项
				this.Fire(timestamp.Value, new[] { schedule });
			}
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
		private void Fire(DateTime timestamp, IEnumerable<ScheduleToken> schedules)
		{
			if(schedules == null)
				return;

			//如果待触发时间有效并且指定要重新触发的时间大于或等于待触发时间则忽略当次调度
			if(_nextTick > 0 && timestamp.Ticks >= _nextTick)
				return;

			//始终创建一个新的任务取消标记源
			var original = Interlocked.Exchange(ref _cancellation, new CancellationTokenSource());

			//如果原有任务标记不为空（表示已经启动过），则将原有任务取消掉
			if(original != null)
				original.Cancel();

			//获取延迟的时长
			var duration = timestamp.Kind == DateTimeKind.Utc ? timestamp - Utility.Now() : timestamp - DateTime.Now;

			//防止延迟时长为负数
			if(duration < TimeSpan.Zero)
				duration = TimeSpan.Zero;

			//更新下次激发的时间点
			_nextTick = timestamp.Ticks;

			Task.Delay(duration).ContinueWith((task, state) =>
			{
				//将最近触发时间点设为此时此刻
				_lastTick = _nextTick;

				//启动新一轮的调度扫描
				this.Scan();

				//设置处理次数
				int count = 0;

				//遍历待执行的调度项集合（该集合没有共享持有者，因此不会有变更冲突尽可放心遍历）
				foreach(var schedule in (IEnumerable<ScheduleToken>)state)
				{
					//遍历当前调度项内的所有处理器集合（该处理器集合已做了同步支持）
					foreach(var handler in schedule.Handlers)
					{
						//创建处理上下文对象
						var context = new HandlerContext(this, schedule.Trigger, count++);

						//调用处理器进行处理（该方法内会屏蔽异常，并对执行异常的处理器进行重发处理），其返回真则表示执行成功
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

				//将失败的处理器加入到重试队列中
				_retriever.Retry(handler, context);

				//返回调用失败
				return false;
			}
		}

		private bool ScheduleCore(IHandler handler, ITrigger trigger)
		{
			//获取指定触发器关联的执行处理器集合
			var schedule = _schedules.GetOrAdd(trigger, key => new ScheduleToken(key, new HashSet<IHandler>()));

			//将指定的执行处理器加入到对应的调度项的执行集合中，如果加入成功则尝试重新激发
			//该新增方法确保同步完成，不会引发线程重入导致的状态不一致
			if(schedule.AddHandler(handler))
			{
				//尝试重新调度
				this.Refire(schedule);

				//返回新增调度成功
				return true;
			}

			//返回默认失败
			return false;
		}
		#endregion

		#region 重试处理
		private void Retriever_Retried(object sender, HandledEventArgs e)
		{
			this.OnHandled(e.Handler, e.Context);
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

					return _handlers.Add(handler);
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

					return _handlers.Remove(handler);
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

					_handlers.Clear();
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

		private class TriggerCollection : IReadOnlyCollection<ITrigger>
		{
			#region 成员字段
			private IDictionary<ITrigger, ScheduleToken> _schedules;
			#endregion

			#region 构造函数
			public TriggerCollection(IDictionary<ITrigger, ScheduleToken> schedules)
			{
				_schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
			}
			#endregion

			#region 公共属性
			public int Count
			{
				get
				{
					return _schedules.Count;
				}
			}
			#endregion

			#region 枚举遍历
			public IEnumerator<ITrigger> GetEnumerator()
			{
				foreach(var schedule in _schedules)
					yield return schedule.Key;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
			#endregion
		}
		#endregion
	}
}
