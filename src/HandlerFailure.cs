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

namespace Zongsoft.Scheduling
{
	/// <summary>
	/// 表示处理器执行失败的重试信息。
	/// </summary>
	public struct HandlerFailure
	{
		#region 公共字段
		/// <summary>
		/// 获取执行重试的次数。
		/// </summary>
		public readonly int Count;

		/// <summary>
		/// 获取最近一次重试的时间。
		/// </summary>
		public readonly DateTime? Timestamp;

		/// <summary>
		/// 获取重试的最后期限，如果为空表示无限制。
		/// </summary>
		public readonly DateTime? Expiration;
		#endregion

		#region 构造函数
		public HandlerFailure(int count, DateTime? timestamp, DateTime? expiration)
		{
			this.Count = count;
			this.Timestamp = timestamp;
			this.Expiration = expiration;
		}
		#endregion
	}
}
