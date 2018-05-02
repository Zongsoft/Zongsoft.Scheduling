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

namespace Zongsoft.Scheduling
{
	public static class Trigger
	{
		#region 静态字段
		private static readonly IDictionary<string, ITriggerBuilder> _builders = new Dictionary<string, ITriggerBuilder>(StringComparer.OrdinalIgnoreCase);
		private static readonly ConcurrentDictionary<string, ITrigger> _triggers = new ConcurrentDictionary<string, ITrigger>(StringComparer.OrdinalIgnoreCase);
		#endregion

		#region 静态属性
		public static IDictionary<string, ITriggerBuilder> Builders
		{
			get
			{
				return _builders;
			}
		}
		#endregion

		#region 静态方法
		public static ITrigger Cron(string expression)
		{
			if(string.IsNullOrWhiteSpace(expression))
				return null;

			return Get("cron", expression);
		}

		public static ITrigger Get(string scheme, string expression)
		{
			if(string.IsNullOrWhiteSpace(scheme))
				throw new ArgumentNullException(nameof(scheme));

			scheme = scheme.Trim();

			return _triggers.GetOrAdd((scheme + ":" + expression), key =>
			{
				if(_builders.TryGetValue(scheme, out var builder))
					return builder.Build(expression);

				throw new InvalidProgramException($"The '{scheme}' trigger builder not found.");
			});
		}
		#endregion
	}
}
