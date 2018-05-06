/*
 *    _____                                ____
 *   /_   /  ____  ____  ____  ____ ____  / __/_
 *     / /  / __ \/ __ \/ __ \/ ___/ __ \/ /_/ /_
 *    / /__/ /_/ / / / / /_/ /\_ \/ /_/ / __  __/
 *   /____/\____/_/ /_/\__  /____/\____/_/ / /_
 *                    /____/               \__/
 *
 * Authors:
 *   钟峰(Popeye Zhong) <zongsoft@qq.com>
 *
 * The MIT License (MIT)
 * 
 * Copyright (C) 2018 Zongsoft Corporation <http://www.zongsoft.com>
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Zongsoft.Scheduling.Examples
{
	public class MyScheduler : Scheduler
	{
		#region 成员字段
		private ConcurrentDictionary<uint, MyHandler> _handlers;
		#endregion

		#region 构造函数
		public MyScheduler()
		{
			_handlers = new ConcurrentDictionary<uint, MyHandler>();
		}
		#endregion

		#region 重写方法
		protected override void OnStart(string[] args)
		{
			//异步加载业务数据并生成调度任务
			this.Initialize();

			//调用基类同名方法
			base.OnStart(args);
		}
		#endregion

		#region 私有方法
		private async Task Initialize()
		{
			await Task.Run(() =>
			{
				//获取可用的任务计划集
				var plans = this.GetPlans(200);

				foreach(var plan in plans)
				{
					//如果任务计划不可用或没有指定定时表达式，则忽略该计划
					if(!plan.Enabled || string.IsNullOrWhiteSpace(plan.CronExpression))
						continue;

					//将指定任务计划加入到调度器中
					this.Schedule(this.GetHandler(plan.PlanId), Trigger.Cron(plan.CronExpression));
				}
			});

			//注意：如果上述生成任务计划不是异步方法，则不需要扫描(Scan)来重新生成调度计划
			this.Scan();
		}

		private IEnumerable<Models.PlanModel> GetPlans(int count)
		{
			//默认Cron表达式为每小时整点一发
			var cron = "0 0 * * * ?";

			for(int i = 0; i < count; i++)
			{
				switch(Common.RandomGenerator.GenerateInt32() % 5)
				{
					case 0:
						//每分钟整点来一发
						cron = "0 * * * * ?";
						break;
					case 1:
						//每5分钟整点来一发
						cron = "0 0/5 * * * ?";
						break;
					case 2:
						//每10分钟整点来一发
						cron = "0 0,10,20,30,40,50 * * * ?";
						break;
					case 3:
						//每30分钟整点来一发
						cron = "0 0,30 * * * ?";
						break;
					case 4:
						//每2个小时整点来一发
						cron = "0 0 0/2 * * ?";
						break;
				}

				yield return new Models.PlanModel((uint)(i + 1), null, cron);
			};
		}

		private MyHandler GetHandler(uint key)
		{
			return _handlers.GetOrAdd(key, id => new MyHandler(id));
		}
		#endregion
	}
}
