﻿using System.Net.Sockets;

namespace Fleck
{
	internal class Sender
	{
		private readonly WebSocketConnection _connection;

		public Sender(WebSocketConnection connection)
		{
			_connection = connection;
		}

		public Socket Socket { get { return _connection.Socket; } }

		public void Send(string data)
		{
			if (!Socket.Connected) return;
			var wrapped = DataFrame.Wrap(data);
			try
			{
				Socket.BeginSend(wrapped, 0, wrapped.Length, SocketFlags.None, r =>
					{
						try
						{
							if (Socket.Connected)
								Socket.EndSend(r);
						}
						catch (SocketException e)
						{
							Log.Error(e.Message);
							_connection.Close();
						}
					}, null);
			}
			catch (SocketException e)
			{
				Log.Error(e.Message);
				_connection.Close();
			}
		}
	}
}