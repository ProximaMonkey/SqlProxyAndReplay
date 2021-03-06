﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using ProductiveRage.SqlProxyAndReplay.DataProviderClient;
using ProductiveRage.SqlProxyAndReplay.DataProviderService.Example;
using ProductiveRage.SqlProxyAndReplay.DataProviderService.ProxyImplementations.PassThrough;
using ProductiveRage.SqlProxyAndReplay.DataProviderService.ProxyImplementations.Replay;
using Xunit;

namespace ProductiveRage.SqlProxyAndReplay.Tests
{
	public static class IntegrationTests
	{
		private const string _sqliteInMemoryCacheConnectionString = "Data Source=:memory:";

		[Fact]
		public static void ProxyAndReplayQueryForInMemorySqliteDatabase()
		{
			var databaseInitialisation = @"
				CREATE TABLE test(id INTEGER, name TEXT);
				INSERT INTO test VALUES (1, 'Bob');
				INSERT INTO test VALUES (2, 'Jack');
			";

			// Create an in-memory database and perform a query that will populate a cache that will allow replaying those queries without having to
			// hit the database. The read-write "DictionaryCache" requires a way to execute SQL (so that it can take a SQL statement and generate a
			// disconnected DataSet to cache the contents of) but we'll only need the read methods of the cache class - so, before throwing away
			// the DictionaryCache reference after loading the data from the db, get a read-only wrapper so that the SQL Runner that the read-write
			// cache requires can be tidied up.
			var namesFromSqlCalls = new List<string>();
			ReadOnlyDictionaryCache readOnlyCache;
			using (var reusableConnection = CreateReusableConnection(databaseInitialisation))
			{
				var cache = new DictionaryCache(new SqliteRunner(reusableConnection), infoLogger: Console.WriteLine);
				var proxy = new SqlProxy(() => reusableConnection, cache.QueryRecorder, cache.ScalarQueryRecorder, cache.NonQueryRowCountRecorder);
				using (var proxyConnection = new RemoteSqlConnectionClient(proxy, proxy.GetNewConnectionId()))
				{
					using (var command = proxyConnection.CreateCommand("SELECT * FROM test"))
					{
						proxyConnection.Open();
						using (var reader = command.ExecuteReader())
						{
							while (reader.Read())
							{
								namesFromSqlCalls.Add(reader.GetString(reader.GetOrdinal("Name")));
							}
						}
					}
				}
				readOnlyCache = new ReadOnlyDictionaryCache(cache);
			}

			// Use the read-only cache to create an alternate IDbConnection instance (a SqlReplayer) that will allow the same SQL statement to be
			// executed and the same data returned, without actually requiring the database
			var namesFromReplayedCalls = new List<string>();
			var replayer = new SqlReplayer(readOnlyCache.DataRetriever, readOnlyCache.ScalarDataRetriever, readOnlyCache.NonQueryRowCountRetriever);
			using (var replayerConnection = new RemoteSqlConnectionClient(replayer, replayer.GetNewConnectionId()))
			{
				replayerConnection.ConnectionString = _sqliteInMemoryCacheConnectionString;
				using (var command = replayerConnection.CreateCommand("SELECT * FROM test"))
				{
					replayerConnection.Open();
					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							namesFromReplayedCalls.Add(reader.GetString(reader.GetOrdinal("Name")));
						}
					}
				}
			}

			var expectedValues = new List<string> { "Bob", "Jack" };
			Assert.Equal(expectedValues, namesFromSqlCalls);
			Assert.Equal(expectedValues, namesFromReplayedCalls);
		}

		[Fact]
		public static void ProxyAndReplayQueryForInMemorySqliteDatabaseViaDapper()
		{
			var databaseInitialisation = @"
				CREATE TABLE test(id INTEGER, name TEXT);
				INSERT INTO test VALUES (1, 'Bob');
				INSERT INTO test VALUES (2, 'Jack');
			";

			// Create an in-memory database and perform a query that will populate a cache that will allow replaying those queries without having to
			// hit the database. The read-write "DictionaryCache" requires a way to execute SQL (so that it can take a SQL statement and generate a
			// disconnected DataSet to cache the contents of) but we'll only need the read methods of the cache class - so, before throwing away
			// the DictionaryCache reference after loading the data from the db, get a read-only wrapper so that the SQL Runner that the read-write
			// cache requires can be tidied up.
			List<string> namesFromSqlCalls;
			ReadOnlyDictionaryCache readOnlyCache;
			using (var reusableConnection = CreateReusableConnection(databaseInitialisation))
			{
				var cache = new DictionaryCache(new SqliteRunner(reusableConnection), infoLogger: Console.WriteLine);
				var proxy = new SqlProxy(() => reusableConnection, cache.QueryRecorder, cache.ScalarQueryRecorder, cache.NonQueryRowCountRecorder);
				using (var proxyConnection = new RemoteSqlConnectionClient(proxy, proxy.GetNewConnectionId()))
				{
					namesFromSqlCalls = proxyConnection.Query<TestRow>("SELECT * FROM test").Select(row => row.Name).ToList();
				}
				readOnlyCache = new ReadOnlyDictionaryCache(cache);
			}

			// Use the read-only cache to create an alternate IDbConnection instance (a SqlReplayer) that will allow the same SQL statement to be
			// executed and the same data returned, without actually requiring the database
			List<string> namesFromReplayedCalls;
			var replayer = new SqlReplayer(readOnlyCache.DataRetriever, readOnlyCache.ScalarDataRetriever, readOnlyCache.NonQueryRowCountRetriever);
			using (var replayerConnection = new RemoteSqlConnectionClient(replayer, replayer.GetNewConnectionId()))
			{
				replayerConnection.ConnectionString = _sqliteInMemoryCacheConnectionString;
				using (var command = replayerConnection.CreateCommand("SELECT * FROM test"))
				{
					namesFromReplayedCalls = replayerConnection.Query<TestRow>("SELECT * FROM test").Select(row => row.Name).ToList();
				}
			}

			var expectedValues = new List<string> { "Bob", "Jack" };
			Assert.Equal(expectedValues, namesFromSqlCalls);
			Assert.Equal(expectedValues, namesFromReplayedCalls);
		}

		[Fact]
		public static void DoNotBeConfusedByRepeatedFieldNames()
		{
			var databaseInitialisation = @"
				CREATE TABLE test(id INTEGER, name TEXT);
				INSERT INTO test VALUES (1, 'Bob');
				INSERT INTO test VALUES (2, 'Jack');
			";

			int? fieldCount = null;
			using (var reusableConnection = CreateReusableConnection(databaseInitialisation))
			{
				var proxy = new SqlProxy(() => reusableConnection, queryRecorder: criteria => { }, scalarQueryRecorder: criteria => { }, nonQueryRowCountRecorder: criteria => { });
				using (var proxyConnection = new RemoteSqlConnectionClient(proxy, proxy.GetNewConnectionId()))
				{
					using (var command = proxyConnection.CreateCommand("SELECT id, id, name FROM Test"))
					{
						proxyConnection.Open();
						using (var reader = command.ExecuteReader())
						{
							while (reader.Read())
							{
								fieldCount = reader.FieldCount;
								break;
							}
						}
					}
				}
			}

			Assert.NotNull(fieldCount);
			Assert.Equal(3, fieldCount.Value);
		}

		private static StaysOpenSqliteConnection CreateReusableConnection(string databaseInitialisationSql)
		{
			if (databaseInitialisationSql == null)
				throw new ArgumentNullException(nameof(databaseInitialisationSql));

			var connection = new StaysOpenSqliteConnection();
			connection.ConnectionString = _sqliteInMemoryCacheConnectionString;
			connection.Open();
			if (databaseInitialisationSql != "")
				connection.Execute(databaseInitialisationSql);
			return connection;
		}

		private sealed class TestRow
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}
