﻿using Netco.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Square.Connect.Model;
using SquareAccess.Exceptions;

namespace SquareAccess.Shared
{
	public class SquareLogger
	{
		private static readonly string _versionInfo;
		private const string CaMark = "Square";
		private const int MaxLogLineSize = 0xA00000; //10mb

		static SquareLogger()
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
			_versionInfo = FileVersionInfo.GetVersionInfo( assembly.Location ).FileVersion;
		}

		public static ILogger Log()
		{
			return NetcoLogger.GetLogger( "SquareLogger" );
		}

		public static void LogTraceException( Exception exception )
		{
			Log().Trace( exception, "{channel} An exception occured. [ver:{version}]", CaMark, _versionInfo );
		}

		public static void LogTraceStarted( string info )
		{
			TraceLog( "Trace Start call", info );
		}

		public static void LogTraceEnd( string info )
		{
			TraceLog( "Trace End call", info );
		}

		public static void LogStarted( string info )
		{
			TraceLog( "Start call", info );
		}

		public static void LogEnd( string info )
		{
			TraceLog( "End call", info );
		}

		public static void LogTrace( Exception ex, string info )
		{
			TraceLog( "Trace info", info );
		}

		public static void LogTrace( string info )
		{
			TraceLog( "Trace info", info );
		}

		public static void LogTraceRetryStarted( int delaySeconds, int attempt, string info )
		{
			info = String.Format( "{0}, Delay: {0}s, Attempt: {1} ", info, delaySeconds, attempt );
			TraceLog( "Trace info", info );
		}

		public static void LogTraceRetryEnd( string info )
		{
			TraceLog( "TraceRetryEnd info", info );
		}

		private static void TraceLog( string type, string info )
		{
			if( info.Length < MaxLogLineSize )
			{
				Log().Trace( "[{channel}] {type}:{info}, [ver:{version}]", CaMark, type, info, _versionInfo );
				return;
			}

			var pageNumber = 1;
			var pageId = Guid.NewGuid();
			foreach( var page in SplitString( info, MaxLogLineSize ) )
			{
				Log().Trace( "[{channel}] page:{page} pageId:{pageId} {type}:{info}, [ver:{version}]", CaMark, pageNumber++, pageId, type, page, _versionInfo );
			}
		}

		private static IEnumerable< string > SplitString( string str, int chunkSize )
		{
			return Enumerable.Range( 0, str.Length / chunkSize )
				.Select( i => str.Substring( i * chunkSize, chunkSize ) );
		}
	}
}
