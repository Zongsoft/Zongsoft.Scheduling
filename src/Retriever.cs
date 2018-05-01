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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Zongsoft.Scheduling
{
	public class Retriever : IRetriever
	{
		#region 事件定义
		public event EventHandler<HandledEventArgs> Failed;
		public event EventHandler<HandledEventArgs> Succeed;
		#endregion

		#region 成员字段
		private CancellationTokenSource _cancellation;
		private readonly ConcurrentQueue<RetryingToken> _queue;
		#endregion

		#region 构造函数
		public Retriever()
		{
			_queue = new ConcurrentQueue<RetryingToken>();
		}
		#endregion

		#region 公共方法
		public void Run()
		{
			var cancellation = _cancellation;

			if(cancellation == null || cancellation.IsCancellationRequested)
			{
				var original = Interlocked.Exchange(ref _cancellation, new CancellationTokenSource());

				if(original != null)
					original.Cancel();
			}

			if(cancellation.IsCancellationRequested)
				Task.Factory.StartNew(OnRetry, cancellation.Token, cancellation.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		}

		public void Stop(bool clean)
		{
			var cancellation = _cancellation;

			if(cancellation != null)
				cancellation.Cancel();

			if(clean)
			{
				while(!_queue.IsEmpty)
				{
					_queue.TryDequeue(out var _);
				}
			}
		}

		public void Retry(IHandler handler, IHandlerContext context)
		{
			//获取下次触发的时间点
			var limit = context.Trigger.GetNextOccurrence();

			//如果下次触发时间点不为空，则表示还有机会触发
			if(limit.HasValue)
			{
				//计算再次触发的间隔时长
				var duration = limit.Value.AddSeconds(1) - DateTime.Now;

				//根据再次触发的间隔时长计算重试的最后期限
				if(duration.TotalDays > 28)
					limit = limit.Value.AddDays(-1);
				else if(duration.TotalDays > 1)
					limit = limit.Value.AddHours(-1);
				else if(duration.TotalHours > 1)
					limit = limit.Value.AddMinutes(-30);
				else
					limit = limit.Value.AddMinutes(-1);
			}

			//将处理器加入到重试队列
			_queue.Enqueue(new RetryingToken(handler, context, limit));

			//如果取消源为空（未启动过重试任务）或已经被取消（即重试任务被中断），则应该重启重试任务
			if(_cancellation == null || _cancellation.IsCancellationRequested)
				this.Run();
		}
		#endregion

		#region 重试处理
		private void OnRetry(object state)
		{
			var cancellation = (CancellationToken)state;

			while(!cancellation.IsCancellationRequested)
			{
				//如果重试队列空了，那就休息一会吧
				if(_queue.IsEmpty)
					Thread.Sleep(1000);

				if(_queue.TryDequeue(out var token))
				{
					//获取待重试的处理器的延迟执行时间
					var latency = this.GetLatency(token);

					//如果延迟执行时间为空则表示已经过期（即再也不用重试了，弃之）
					if(latency == null)
						continue;

					//如果延迟时间大于当前时间，则应跳过当前项
					if(latency.Value > DateTime.Now)
					{
						//如果重试队列空了，则表示该执行项很可能是唯一需要重试项
						//那么需要暂停一秒（重试最小间隔单位），以避免频繁空处理
						if(_queue.IsEmpty)
							Thread.Sleep(1000);

						//将该未到延迟执行的处理器重新入队
						_queue.Enqueue(token);

						//继续队列中下一个
						continue;
					}

					var isFailed = false;

					try
					{
						//更新上下文中的重试信息
						token.Context.Failure = new HandlerFailure(token.RetriedCount, token.RetriedTimestamp, token.Expiration);

						//调用处理器的处理方法
						token.Handler.Handle(token.Context);
					}
					catch
					{
						//标示重试失败
						isFailed = true;

						//将重试失败的句柄重新入队
						_queue.Enqueue(token);
					}

					//递增重试次数
					token.RetriedCount++;

					//更新处理器的最后重试时间
					token.RetriedTimestamp = DateTime.Now;

					//激发重试失败或成功的事件
					if(isFailed)
						this.OnFailed(token.Handler, token.Context);
					else
						this.OnSucceed(token.Handler, token.Context);
				}
			}
		}
		#endregion

		#region 激发事件
		protected virtual void OnFailed(IHandler handler, IHandlerContext context)
		{
			var e = this.Failed;

			if(e != null)
				Task.Run(() => e(this, new HandledEventArgs(handler, context)));
		}

		protected virtual void OnSucceed(IHandler handler, IHandlerContext context)
		{
			var e = this.Succeed;

			if(e != null)
				Task.Run(() => e(this, new HandledEventArgs(handler, context)));
		}
		#endregion

		#region 私有方法
		private DateTime? GetLatency(RetryingToken token)
		{
			//如果待重试的处理器重试期限已过，则返回空（即忽略它）
			if(token.Expiration.HasValue && token.Expiration < DateTime.Now)
				return null;

			//如果重试次数为零或最后重试时间为空则返回当前时间（即不需要延迟）
			if(token.RetriedCount < 1 || token.RetriedTimestamp == null)
				return DateTime.Now;

			var seconds = Math.Min(token.RetriedCount * 2, 60);
			var latency = token.RetriedTimestamp.Value.AddSeconds(seconds);

			//如果待重试项有最后期限时间并且计算后的延迟执行时间大于该期限值，则返回期限时
			if(token.Expiration.HasValue && latency > token.Expiration.Value)
				return token.Expiration.Value.AddSeconds(-1);

			return latency;
		}
		#endregion

		#region 嵌套子类
		private class RetryingToken
		{
			public IHandler Handler;
			public IHandlerContext Context;
			public DateTime? Expiration;
			public DateTime? RetriedTimestamp;
			public int RetriedCount;

			public RetryingToken(IHandler handler, IHandlerContext context, DateTime? expiration)
			{
				this.Handler = handler;
				this.Context = context;
				this.Expiration = expiration;
			}
		}
		#endregion
	}
}
