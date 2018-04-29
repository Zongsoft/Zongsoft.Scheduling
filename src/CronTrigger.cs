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

namespace Zongsoft.Scheduling
{
	public class CronTrigger : ITrigger, IEquatable<ITrigger>
	{
		#region 成员字段
		private Cronos.CronExpression _expression;
		#endregion

		#region 构造函数
		public CronTrigger(string expression)
		{
			if(string.IsNullOrWhiteSpace(expression))
				throw new ArgumentNullException(nameof(expression));

			_expression = Cronos.CronExpression.Parse(expression, Cronos.CronFormat.IncludeSeconds);
			this.Expression = _expression.ToString();
		}
		#endregion

		#region 公共属性
		public string Expression
		{
			get;
		}
		#endregion

		#region 公共方法
		public bool Equals(ITrigger other)
		{
			return (other is CronTrigger cron) && cron._expression.Equals(_expression);
		}

		public DateTime? GetNextOccurrence(bool inclusive = false)
		{
			return _expression.GetNextOccurrence(Utility.Now(), inclusive);
		}

		public DateTime? GetNextOccurrence(DateTime origin, bool inclusive = false)
		{
			return _expression.GetNextOccurrence(Utility.Now(origin), inclusive);
		}
		#endregion
	}
}
