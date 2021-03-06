﻿using System;
using System.Collections;
using System.Data;
using ProductiveRage.SqlProxyAndReplay.DataProviderInterface.IDs;
using ProductiveRage.SqlProxyAndReplay.DataProviderInterface.Interfaces;

namespace ProductiveRage.SqlProxyAndReplay.DataProviderClient
{
	public sealed class RemoteSqlParameterSetClient : IDataParameterCollection
	{
		private readonly IRemoteSqlCommand _command;
		private readonly IRemoteSqlParameterSet _parameters;
		private readonly IRemoteSqlParameter _parameter;
		private readonly CommandId _commandId;
		public RemoteSqlParameterSetClient(IRemoteSqlCommand command, IRemoteSqlParameterSet parameters, IRemoteSqlParameter parameter, CommandId commandId)
		{
			if (command == null)
				throw new ArgumentNullException(nameof(command));
			if (parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			if (parameter == null)
				throw new ArgumentNullException(nameof(parameter));

			_command = command;
			_parameters = parameters;
			_parameter = parameter;
			_commandId = commandId;

			SyncRoot = new object();
		}

		public object this[int index]
		{
			get { return new RemoteSqlParameterClient(_parameter, _parameters.GetParameterByIndex(_commandId, index), _commandId); }
			set
			{
				// Note: The type of this property is object (and not something more specific, like IDbDataParameter) because IDataParameterCollection implements
				// IList, which has this property it has to throw at runtime if an unacceptable value is provided
				var parameter = GetAsRemoteSqlParameterClient(value);
				if (!parameter.CommandId.Equals(_commandId))
				{
					// The server has to keep track of what parameters are owned by what commands for book-keeping purposes, allow parameters to be created by
					// one command and used by others would make that much more complicated and so it's not supported (hopefully it's an edge case and not used
					// by any real code, even if it's possible with the SqlCommand class)
					throw new ArgumentException($"A parameter that is initialised via a call to CreateParameter on one {typeof(RemoteSqlCommandClient)} may not be reassigned for use on a different instance");
				}
				_parameters.SetParameterByIndex(_commandId, index, parameter.ParameterId);
			}
		}

		public object this[string parameterName]
		{
			get { return new RemoteSqlParameterClient(_parameter, _parameters.GetParameterByName(_commandId, parameterName), _commandId); }
			set
			{
				// Note: The type of this property is object (and not something more specific, like IDbDataParameter) because IDataParameterCollection implements
				// IList, which has this property it has to throw at runtime if an unacceptable value is provided
				if (parameterName == null)
					throw new ArgumentNullException(nameof(parameterName));
				var parameter = GetAsRemoteSqlParameterClient(value);
				if (!parameter.CommandId.Equals(_commandId))
				{
					// The server has to keep track of what parameters are owned by what commands for book-keeping purposes, allow parameters to be created by
					// one command and used by others would make that much more complicated and so it's not supported (hopefully it's an edge case and not used
					// by any real code, even if it's possible with the SqlCommand class)
					throw new ArgumentException($"A parameter that is initialised via a call to CreateParameter on one {typeof(RemoteSqlCommandClient)} may not be reassigned for use on a different instance");
				}
				_parameters.SetParameterByName(_commandId, parameterName, parameter.ParameterId);
			}
		}

		public int Count
		{
			get { return _parameters.GetCount(_commandId); }
		}

		public int Add(object value)
		{
			// Note: This method has this signature as IDataParameterCollection implements IList, which has this method (which is why value does not have
			// a more specific type - it has to throw at runtime if an unacceptable value is provided)
			var parameter = GetAsRemoteSqlParameterClient(value);
			if (!parameter.CommandId.Equals(_commandId))
			{
				// The server has to keep track of what parameters are owned by what commands for book-keeping purposes, allow parameters to be created by
				// one command and used by others would make that much more complicated and so it's not supported (hopefully it's an edge case and not used
				// by any real code, even if it's possible with the SqlCommand class)
				throw new ArgumentException($"A parameter that is initialised via a call to CreateParameter on one {typeof(RemoteSqlCommandClient)} may not be reassigned for use on a different instance");
			}
			return _parameters.Add(_commandId, parameter.ParameterId);
		}

		// This method isn't part of IDataParameterCollection but it's on the SqlParameterCollection and it's very convenient, so I'm including it here
		public IDbDataParameter AddWithValue(string parameterName, object value)
		{
			if (string.IsNullOrWhiteSpace(parameterName))
				throw new ArgumentException($"Null/blank {nameof(parameterName)} specified");

			var parameter = new RemoteSqlParameterClient(_parameter, _command.CreateParameter(_commandId), _commandId);
			parameter.ParameterName = parameterName;
			parameter.Value = value;
			Add(parameter);
			return parameter;
		}

		public void Clear() { _parameters.Clear(_commandId); }

		public bool Contains(object value)
		{
			var parameter = GetAsRemoteSqlParameterClient(value);
			return _parameters.Contains(_commandId, parameter.ParameterId);
		}

		public bool Contains(string parameterName)
		{
			if (parameterName == null)
				throw new ArgumentNullException(nameof(parameterName));
			return _parameters.Contains(_commandId, parameterName);
		}

		public void CopyTo(Array array, int index)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (index < 0)
				throw new IndexOutOfRangeException();

			var numberOfParameters = _parameters.GetCount(_commandId);
			for (var i = 0; i < numberOfParameters; i++)
			{
				var parameterId = _parameters.GetParameterByIndex(_commandId, i);
				var arrayIndex = i + index;
				if (arrayIndex >= array.Length)
					throw new ArgumentException("Destination array was not long enough.Check destIndex and length, and the array's lower bounds");
				array.SetValue(new RemoteSqlParameterClient(_parameter, parameterId, _commandId), arrayIndex);
			}
		}

		public IEnumerator GetEnumerator() { return new RemoteSqlParameterSetClientEnumerator(this); }

		public int IndexOf(object value)
		{
			// Note: This method has this signature as IDataParameterCollection implements IList, which has this method (which is why value does not have
			// a more specific type - it has to throw at runtime if an unacceptable value is provided)
			var parameter = GetAsRemoteSqlParameterClient(value);
			if (!parameter.CommandId.Equals(_commandId))
			{
				// The server has to keep track of what parameters are owned by what commands for book-keeping purposes, allow parameters to be created by
				// one command and used by others would make that much more complicated and so it's not supported (hopefully it's an edge case and not used
				// by any real code, even if it's possible with the SqlCommand class)
				throw new ArgumentException($"A parameter that is initialised via a call to CreateParameter on one {typeof(RemoteSqlCommandClient)} may not be reassigned for use on a different instance");
			}
			return _parameters.IndexOf(_commandId, parameter.ParameterId);
		}

		public int IndexOf(string parameterName)
		{
			return _parameters.IndexOf(_commandId, parameterName);
		}

		public void Insert(int index, object value)
		{
			// Note: This method has this signature as IDataParameterCollection implements IList, which has this method (which is why value does not have
			// a more specific type - it has to throw at runtime if an unacceptable value is provided)
			var parameter = GetAsRemoteSqlParameterClient(value);
			if (!parameter.CommandId.Equals(_commandId))
			{
				// The server has to keep track of what parameters are owned by what commands for book-keeping purposes, allow parameters to be created by
				// one command and used by others would make that much more complicated and so it's not supported (hopefully it's an edge case and not used
				// by any real code, even if it's possible with the SqlCommand class)
				throw new ArgumentException($"A parameter that is initialised via a call to CreateParameter on one {typeof(RemoteSqlCommandClient)} may not be reassigned for use on a different instance");
			}
			_parameters.Insert(_commandId, index, parameter.ParameterId);
		}

		public void Remove(object value)
		{
			// Note: This method has this signature as IDataParameterCollection implements IList, which has this method (which is why value does not have
			// a more specific type - it has to throw at runtime if an unacceptable value is provided)
			var parameter = GetAsRemoteSqlParameterClient(value);
			if (!parameter.CommandId.Equals(_commandId))
			{
				// The server has to keep track of what parameters are owned by what commands for book-keeping purposes, allow parameters to be created by
				// one command and used by others would make that much more complicated and so it's not supported (hopefully it's an edge case and not used
				// by any real code, even if it's possible with the SqlCommand class)
				throw new ArgumentException($"A parameter that is initialised via a call to CreateParameter on one {typeof(RemoteSqlCommandClient)} may not be reassigned for use on a different instance");
			}
			_parameters.Remove(_commandId, parameter.ParameterId);
		}

		public void RemoveAt(int index)
		{
			_parameters.RemoveAt(_commandId, index);
		}

		public void RemoveAt(string parameterName)
		{
			_parameters.RemoveAt(_commandId, parameterName);
		}

		// These properties are only required to the various interfaces that IDataParameterCollection implements (IList and ICollection), they're not very
		// important or relevant to the implementation of a parameter set
		public bool IsFixedSize { get { return false; } }
		public bool IsReadOnly { get { return false; } }
		public bool IsSynchronized { get { return false; } } // This isn't hugely important since this approach to thread-safety is obsolete now
		public object SyncRoot { get; } // This isn't hugely important since this approach to thread-safety is obsolete now

		/// <summary>
		/// This will throw an exception for a null value or one that is not a RemoteSqlParameterClient
		/// </summary>
		private static RemoteSqlParameterClient GetAsRemoteSqlParameterClient(object value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));
			var parameter = value as RemoteSqlParameterClient;
			if (parameter == null)
				throw new ArgumentException($"must be an RemoteSqlParameterClient implementation, not a {value.GetType()}", nameof(value));
			return parameter;
		}

		private sealed class RemoteSqlParameterSetClientEnumerator : IEnumerator
		{
			private readonly RemoteSqlParameterSetClient _parameters;
			private int _index;
			public RemoteSqlParameterSetClientEnumerator(RemoteSqlParameterSetClient parameters)
			{
				if (parameters == null)
					throw new ArgumentNullException(nameof(parameters));
				_parameters = parameters;
				_index = -1;
			}

			public object Current
			{
				get
				{
					if (_index == -1)
						throw new InvalidOperationException("Enumeration has not started. Call MoveNext.");
					if (_index >= _parameters.Count)
						throw new InvalidOperationException("Enumeration already finished.");
					return _parameters[_index];
				}
			}

			public bool MoveNext()
			{
				if (_index >= _parameters.Count)
					return false;
				_index++;
				return _index < _parameters.Count;
			}

			public void Reset()
			{
				_index = -1;
			}
		}
	}
}
