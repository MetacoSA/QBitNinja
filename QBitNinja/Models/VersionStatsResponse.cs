using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Models
{
	public class VersionStatsResponse
	{
		public VersionStats Last144
		{
			get;
			set;
		}
		public VersionStats Last2016
		{
			get;
			set;
		}
		public VersionStats SincePeriodStart
		{
			get;
			set;
		}
	}

	public class VersionStats
	{
		public VersionStats()
		{
			Stats = new List<VersionStatsItem>();
		}
		public int Total
		{
			get;
			set;
		}
		public int FromHeight
		{
			get; set;
		}
		public int ToHeight
		{
			get; set;
		}

		public List<VersionStatsItem> Stats
		{
			get;
			set;
		}
	}

	public class VersionStatsItem
	{
		public int Version
		{
			get;
			set;
		}
		public int Count
		{
			get;
			set;
		}
		public double Percentage
		{
			get;
			set;
		}
	}
}
